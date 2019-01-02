---
layout: post
title: Creating AWS CloudFormation Custom Resources using C# and Lambda
author: Richard Nagle
excerpt: Extend the abilities of AWS CloudFormation by using custom resources built in Lambda using C#.
tags: aws,lambda,serverless,c#,cloudformation
---

## Introduction
CloudFormation is AWS's infrastructure-as-code service allowing you to describe, provision and maintain resources for your AWS infrastructure. Within CloudFormation infrastructure is described using JSON or YAML documents known as *templates*; templates are made up of *resources* each one describing a *resource type* which is the AWS service being provisioned; once complete you submit your template to the CloudFormation service to create a *stack*. The following example template contains two resources: a single EC2 instance and an internal DNS record for the instances using the AWS Route 53 service.

```json
{
  "Description": "Provisions webserver001 and assigns DNS",
  "AWSTemplateFormatVersion": "2010-09-09",
  "Resources": {
    "Webserver001": {
      "Type": "AWS::EC2::Instance",
      "Properties": {
        "InstanceType": "t3.medium",
        "SubnetId": "subnet-12345678",
        "ImageId": "ami-1234567890abcef12",
      }
    },
    "Webserver001Dns": {
      "Type": "AWS::Route53::RecordSet",
      "Properties": {
        "Type": "A",
        "Name": "webserver001.internal.company.com.",
        "HostedZoneName": "internal.company.com.",
        "ResourceRecords": [
          {
            "Fn::GetAtt": ["Webserver001", "PrivateIp"]
          }
        ],
        "TTL": "300",
      }
    }
  }
}
```

While the CloudFormation team do a great job at providing resource types for the vast majority of the huge selection of services AWS offers, sometimes you find a missing resource type or a missing property on an existing type preventing you from using CloudFormation, this is particularly true of new services where CloudFormation hasn't had time to catch up yet. This was a scenario I faced recently trying to provision a [Transit Gateway](https://aws.amazon.com/transit-gateway/) which is a newly released service which simplifies cross-VPC and VPN network infrastructure. Whilst I could find resource types to create the Transit Gateway and attach it to a VPC, there was no way to create a route within the VPC to the Transit Gateway; the existing Route type `AWS::EC2::Route` was missing the `TransitGatewayId` property.

For such eventualities you can use CloudFormation Custom Resources to execute custom code to provision infrastructure using the AWS SDK.

## Getting Started
The first thing we need to do is to create the custom resource within a CloudFormation template. This is done in the standard way by creating a resource with the `AWS::CloudFormation::CustomResource` resource type; and then adding a single property named `ServiceToken` which contains the endpoint CloudFormation will execute to create this resource. Two types of endpoints are supported by CloudFormation: SNS and Lambda functions.

In this post we're looking at Lambda endpoints, so head over to the Lambda console and create the function definition:

* Select `[Create function]` and enter the following options:
  * Name: `CreateTransitGatewayRouteCustomResource`
  * Runtime: `.NET Core 2.1 (C#/Powershell)`
  * Role: `Create a new role from one or more templates`
  * Role name: `CreateTransitGatewayRouteCustomResourceRole`
  * Policy templates: *Leave blank*
* Then press the `[Create function]` button.

This creates an empty lambda with no code attached, we'll get back to creating the function later. Meanwhile, back on the Lambda console, in the top-right-hand corner is the ARN for this function; this is the endpoint value we require for our CloudFormation template. Copy this ARN and paste it into the template as the `ServiceToken` value.

Finally, we can add additional properties to the custom resource to configure it. Later we'll see that these additional values get passed to our Lambda function for its use in creating the resource. In our case we need additional properties for the destination IP range to route, the transit gateway to route to, and the route table to which the route is added.

Our final resource looks something like this
```json
{
  "RouteToTransitGateway": {
    "Type": "AWS::CloudFormation::CustomResource",
    "Properties": {
      "ServiceToken": "arn:aws:lambda:eu-west-2:999999999999:function:CreateTransitGatewayRouteCustomResource",
      "DestinationCidrBlock": "192.168.0.0/16",
      "TransitGatewayId": { "Ref": "TransitGateway" },
      "RouteTableId": { "Ref": "PublicRouteTable"}
    }
  }
}
```
The `Ref` values for `TransitGatewayId` and `RouteTableId` provide references to other resources created in the same template. When the stack is provisioned the `Ref` is replaced by the actually Id of the referenced resource once created; and it is these actual IDs which are passed onto Lambda. If you prefer, you can replace these `Ref` values with literal IDs for existing resources.

## Understanding how Custom Resources are Provisioned

![Schematic of process](/images/CloudFormation-Custom-Resource.png)

1. CloudFormation creates a *JSON Request*  object containing information about the stack and the properties (except `ServiceToken`) in the custom resource. The properties in the JSON Request are as follows:

    `RequestType`<br/>
    This is going to be `Create`, `Update` or `Delete` to indicate which operation CloudFormations want to perform on the resource. For this post we're only going to concentrate on creating resources but you need to be aware that CloudFormation can update and delete resources and you need to cater for that.

    `ResponseURL`<br/>
    This is a URL you'll respond to tell CloudFormation that the resource creation process is complete.

    `StackId`<br/>
    The ARN (Amazon Resource Name) identifying the stack under construction.

    `RequestId`<br/>
    Unique identifier for the request

    `ResourceType`<br/>
    The type of the resource being requested, in our case this is `AWS::CloudFormation::CustomResource`. Another option when creating custom resources is to set the type in the template to `Custom::<Name>` (for example, we could have used `Custom::RouteToTransitGateway`). Whichever method you use for your custom resources type, that is the value used here.

    `LogicalResourceId`<br/>
    The name used in the CloudFormation template for the resource requested. In our case this will be `RouteToTransitGateway`.

    `ResourceProperties`<br/>
    A component object containing the additional custom properties contained in the template.

    For more information see [Custom Resource Request Objects](https://docs.aws.amazon.com/AWSCloudFormation/latest/UserGuide/crpg-ref-requests.html) in the AWS documentation.

    For our example, the JSON Request object will look like

    ```json
    {
      "RequestType": "Create",
      "ResponseURL": "http:\\some-url-provided-by-cloud-formation",
      "StackId": "arn:aws:cloudformation:eu-west-2:999999999999:stack/MyStackName/8cadb940-1369-11e9-baa6-066164e595fc",
      "RequestId": "65a31be0-b850-4859-809a-3ed1ffe477e8",
      "ResourceType": "AWS::CloudFormation::CustomResource",
      "LogicalResourceId": "RouteToTransitGateway",
      "ResourceProperties": {
          "DestinationCidrBlock": "192.168.0.0/16",
          "TransitGatewayId": "tgw-0397a367e2ea362bd",
          "RouteTableId": "rtb-07203aac63ff26d43"
      }
    }
    ```

2. CloudFormation requests the Lambda service to execute the function identified by the `ServiceToken` property in the template. The JSON Request object is passed as the parameter to the function.

3. The Lambda function executes and creates the resources requested in the Json Request object.

4. The Lambda function creates a *JSON Response* object containing the following properties:

    `Status`<br/>
    Must be either `SUCCESS` or `FAILED` to indicate if the resource was created successfully or not.

    `Reason`<br/>
    Only required when `Status` is `Failed`; this property describes why resource creation failed. This message is displayed in the CloudFormation console and logs.

    `PhysicalResourceId`<br/>
    This is an ID used to identify the physical resource created. Any future request to update or delete the resource will send this value in the `PhysicalResourceId` of the JSON Request so that the Lambda function knows which resource to update or delete.

    `StackId`<br/>
    This value is copied from `StackId` in the Request JSON object.

    `RequestId`<br/>
    This value is copied from `RequestId` in the Request JSON object.

    `LogicalResourceId`<br/>
    This value is copied from `LogicalRequestId` in the Request JSON object.

    `Data`<br/>
    An optional collection of key-value pairs containing information about the created resource. The items in `Data` can be referenced by other resources in the same CloudFormation template using the `Fn::GetAtt` function. For example, suppose you create a custom resource named `CatalogApi` in the template, and the Lambda function returns `Data` containing an item named `Uri`, another resource in that template could use `{Fn::GetAtt: ["CatalogApi", "Uri"]}` to obtain the value.

    For more information see [Custom Resource Response Objects](https://docs.aws.amazon.com/AWSCloudFormation/latest/UserGuide/crpg-ref-responses.html) in the AWS documentation.

    The JSON Response object will look like

    ```json
    {
      "Status": "SUCCESS",
      "Reason": "",
      "StackId": "arn:aws:cloudformation:eu-west-2:999999999999:stack/MyStackName/8cadb940-1369-11e9-baa6-066164e595fc",
      "RequestId": "65a31be0-b850-4859-809a-3ed1ffe477e8",
      "LogicalResourceId": "RouteToTransitGateway",
      "PhysicalResourceId": "rtb-07203aac63ff26d43::192.168.0.0/16"
    }
    ```
    NB We're not returning any `Data`

5. The Lambda function makes an HTTP `PUT` request to the endpoint specified by the `ResponseURL` property in the JSON Request object. This notifies CloudFormation that the resource has now been created and it can carry on creating other resources as required. Alternatively it can tell CloudFormation that the operation failed, upon which CloudFormation will rollback the stack.

## Creating the Lambda Function

So now we understand what needs to be built let start building it. In my previous post [Getting Started using AWS Lambda with C#](({% post_url 2019-01-02-getting-started-using-aws-lambda-using-c# %})) I explained how to get set-up for C# Lambda development. If you have not already done so, go there and set your machine up.

Start by creating a shell Lambda project:
```
md \cloudformation.transitgateway.route
cd \cloudformation.transitgateway.route
dotnet new lambda.emptyfunction
```
Go to the `.\src\cloudformation.transitgateway.route` folder which is where we'll be creating the function. First thing is to update the `aws-lambda-tools-defaults.json` to configure the deployment to the Lambda service.

1. Change the value of `region` to whatever AWS region you earlier created the Lambda function in.
2. Add a new line `"function-name": "CreateTransitGatewayCustomResource"` which defines the Lambda function to deploy to (the one we created earlier at the Lambda console).

Now to test the setup, the following should execute without error:
```
dotnet lambda deploy-function
```

## Creating the Transit Gateway Route

The first thing we need is a class to deserialise the JSON Request object into. In fact we'll create two classes, `CloudFormationRequest` to hold the generic CloudFormation data and `RouteToTransitGateway` to contain the specific custom properties for creating a route to the Transit Gateway.

```c#
public class CloudFormationRequest<T>
{
    public string RequestType { get; set; }
    public string ResponseURL { get; set; }
    public string StackId { get; set; }
    public string RequestId { get; set; }
    public string ResourceType { get; set; }
    public string LogicalResourceId { get; set; }
    public T ResourceProperties { get; set; }
}

public class RouteToTransitGateway
{
    public string DestinationCidrBlock { get; set; }
    public string RouteTableId { get; set; }
    public string TransitGatewayId { get; set; }
}
```

We can then plug these types together as the `input` to the Lambda function handler in `Function.cs` as so

```c#
public async Task FunctionHandler(CloudFormationRequest<RouteToTransitGateway> input)
{}
```

Then we add some code to create the route using the using the AWS SDK. In order to make this work you'll need to add the NuGet package `AWSSDK.EC2` to the project.

```c#
var request = new CreateRouteRequest
{
    DestinationCidrBlock = input.ResourceProperties.DestinationCidrBlock,
    TransitGatewayId = input.ResourceProperties.TransitGatewayId,
    RouteTableId = input.ResourceProperties.RouteTableId
};

using (var client = new AmazonEC2Client())
{
    await client.CreateRouteAsync(request);
}
```

One final thing we need to do is to authorise the Lambda function to be able to create routes. When we created the Lambda function definition earlier, we set the Role to `CreateTransitGatewayRouteCustomResourceRole`; it's this role which defines what the Lambda function is allowed to do within AWS. To update the role we go to the AWS IAM console:

* Click on `Roles` from the left-hand navigation
* Click on the `CreateTransitGatewayRouteCustomResourceRole` link
* Click on `Add inline policy`
* Select the `JSON` tab and replace the contents with the following

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "VisualEditor0",
      "Effect": "Allow",
      "Action": [
        "ec2:CreateRoute",
        "ec2:DeleteRoute"
       ],
       "Resource": "*"
    }
  ]
}
```
* Press the `[Review policy]` button
* Name the policy as `Manage-EC2-Routes`
* Press the `[Create policy]` button

NB This also allows deletion of routes which you'll require later when you implement update and deletion of the resource.

## Responding to CloudFormation

So now, the final thing left to do is send the HTTP `PUT` response back to CloudFormation. Firstly we need a response class for serialisation to JSON.

```c#
public class CloudFormationResponse
{
    public string Status { get; set; }
    public string Reason { get; set; }
    public string PhysicalResourceId { get; set; }
    public string StackId { get; set; }
    public string RequestId { get; set; }
    public string LogicalResourceId { get; set; }
}
```

Which is populated thus:

```c#
var response = new CloudFormationResponse
{
    Status = "SUCCESS",
    Reason = "",
    PhysicalResourceId = $"{request.ResourceProperties.RouteTableId}::{request.ResourceProperties.DestinationCidrBlock}",
    StackId = request.StackId,
    RequestId = request.RequestId,
    LogicalResourceId = request.LogicalResourceId,
}
```

Then we can send the response using `HttpClient` to the `ResponseURL` contained in the original request.

```c#
var jsonContent = new StringContent(JsonConvert.SerializeObject(response));
jsonContent.Headers.Remove("Content-Type");

using (var client = new HttpClient())
{
    var postResponse = await client.PutAsync(url, jsonContent);
    postResponse.EnsureSuccessStatusCode();
}
```

And that's it folks. All that's left to do is deploy the function to lambda with `dotnet lambda deploy-function` and then go test it by creating a stack in the CloudFormation console.

## What's Left?

There's still a few things left to do, which I'll leave as an exercise for the reader:

* Handling CloudFormation requests to delete and update the resource
* Error handling - if the resource creation fails we need to send notification back to CloudFormation
* Unit tests

A complete example showing all of the above can be found on [GitHub](https://github.com/richardnagle/cloudformation-custom-resources).