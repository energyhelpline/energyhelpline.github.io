---
layout: post
title: Getting Started with AWS Lambda using C#
author: Richard Nagle
excerpt: Get started guide to creating serverless application in AWS Lambda using C#.
tags: aws,lambda,serverless,c#
---

## Introduction
Lambda is an AWS service for running code without having to provision specific hardware to do so, hence serverless. From your point-of-view Lambda is infinitely scalable, always-on, and highly available. It is also incredibly cheap when compared to the cost of running a fleet of EC2 instances; and considerably simpler to set-up.

In this article I am going to show how to use C# and .NET core to write Lambda functions.

## Lambda Set-up
To set up Lambda development in C# on your machine you need to install a few things.

1. [.NET Core SDK Version 2.1.300 or greater](https://dotnet.microsoft.com/download/dotnet-core/2.1)

2. AWS Powershell module
```
Install-Module -Name AWSPowerShell
```

3. Amazon.Lambda.Tools .NET Core Global Tool (`dotnet lambda`)
```
dotnet tool install -g Amazon.Lambda.Tools
```

4. Lambda templates for `dotnet new`
```
dotnet new -i Amazon.Lambda.Templates
```

Next you'll need to set up your AWS credentials profile for .NET. In order to be able to do this you'll need a set of Access Keys, see the AWS document [AWS Account and Access Keys](https://docs.aws.amazon.com/powershell/latest/userguide/pstools-appendix-sign-up.html) to do this. Then
```
Set-AWSCredentials -AccessKey <access key> -SecretKey <secret key> -StoreAs default
```

After installing restart your shell.

## Anatomy of a Lambda Function

Firstly, we'll use the `dotnet` CLI to create a basic sample Lambda project.
```
md \lambda.basic
cd \lambda.basic
dotnet new lambda.emptyfunction
```
Unit tests are in the `tests` folder, and the actual function is in the `src` folder. We'll ignore the tests for now and dive straight into the `src` folder.

```
cd .\src\lambda.basic
```
This folder contains a standard C# project used for creating the Lambda function. We'll look at the code in a minute, but first open the file `aws-lambda-tools-defaults.json`. This is a file used to configure the deployment of your C# code to the Lambda service. We need to make a couple of changes here:

1. Change the value of `region` to whatever AWS region you want to create the Lambda function in, such as `eu-west-1`.
2. Add a new line `"function-name": "BasicExample"` which defines the Lambda function name we'll create in AWS.

Now go ahead and open the `Function.cs` file where you'll find the following code:
```c#
    public class Function
    {
        public string FunctionHandler(string input, ILambdaContext context)
        {
            return input?.ToUpper();
        }
    }
```
This is the *Lambda handler*, the code which the Lambda service will execute. It is a function that receives some input in the first parameter; and optionally some context about the Lambda execution in the second parameter. The input is always passed from the Lambda service as a JSON object and deserialised into the `input` parameter. The lambda handler can return a value, but this is only really useful for debugging when running from the Lambda console or CLI.

If you look back at the `aws-lambda-tools-defaults.json` file, you'll see there is a line
```
"function-handler" : "lambda.basic::lambda.basic.Function::FunctionHandler"
```
This tells the Lambda service how to find the Lambda handler. It is in the format
```
<assembly-name>::<fully-qualified-class-name>::<method-name>
```

For more information see [AWS Lambda Function Handler in C#](https://docs.aws.amazon.com/lambda/latest/dg/dotnet-programming-model-handler-types.html).

## Deploying the Function

We'll now go ahead and deploy this basic function to the Lambda service. We do this using the following `dotnet` CLI command:

```
dotnet lambda deploy-function
```

This builds and publishes the project, then zips it up and finally uploads it to the Lambda function we specified earlier in the `aws-lambda-tools-defaults.json` file; in our case `BasicExample`. Because we are creating a new function the CLI will ask us a few details about the function. Firstly it asks:

```
Select IAM Role that to provide AWS credentials to your code:
    1) Some-Existing-Role
    2) Another-Existing-Role
    3) *** Create new IAM Role ***
```

Choose the option for `*** Create new IAM Role ***`. Then  you are asked

```
Enter name of the new IAM Role:
```

Enter `BasicExampleRole`.  Finally you are asked:
```
Select IAM Policy to attach to the new role and grant permissions
    1) AWSLambdaFullAccess (Provides full access to Lambda, S3, DynamoDB, CloudWatch Metrics and  ...)
    2) AWSLambdaReplicator
    3) AWSLambdaDynamoDBExecutionRole (Provides list and read access to DynamoDB streams and writ ...)
    4) AWSLambdaExecute (Provides Put, Get access to S3 and full access to CloudWatch Logs.)
    5) AWSLambdaSQSQueueExecutionRole (Provides receive message, delete message, and read attribu ...)
    6) AWSLambdaKinesisExecutionRole (Provides list and read access to Kinesis streams and write  ...)
    7) AWSLambdaReadOnlyAccess (Provides read only access to Lambda, S3, DynamoDB, CloudWatch Met ...)
    8) AWSLambdaBasicExecutionRole (Provides write permissions to CloudWatch Logs.)
    9) AWSLambdaInvocation-DynamoDB (Provides read access to DynamoDB Streams.)
   10) AWSLambdaVPCAccessExecutionRole (Provides minimum permissions for a Lambda function to exe ...)
   11) AWSLambdaRole (Default policy for AWS Lambda service role.)
   12) *** No policy, add permissions later ***
```

Select the option for `AWSLambdaExecute`. These settings are setting up the security profile that the Lambda function will execute in; we have set it up with the most basic permissions that only allow Lambda execution without any access to other AWS services. If you were writing a Lambda to interact with other AWS services you'd need to add appropriate policies to the role to enable this.

Once the function has been deployed correctly you will see the message

```
New Lambda function created
```

## Invoking the Function

Now let's test this function when run on Lambda. To do this we can use the CLI as follows:
```
dotnet lambda invoke-function --payload "hello world"
```
This will invoke the function using the AWS Lambda service. The `--payload` argument specifies the JSON value to be used as the input into the Lambda function; it can be a constant as shown here or the name of a file containing test JSON.

The output should include the payload `HELLO WORLD` which is the result of the function along with some metrics for the execution.

```
Amazon Lambda Tools for .NET Core applications (3.1.1)
Project Home: https://github.com/aws/aws-extensions-for-dotnet-cli, https://github.com/aws/aws-lambda-dotnet

Payload:
"HELLO WORLD"

Log Tail:
START RequestId: 42db1fb6-0ea5-11e9-92a9-89a0363e40cc Version: $LATEST
END RequestId: 42db1fb6-0ea5-11e9-92a9-89a0363e40cc
REPORT RequestId: 42db1fb6-0ea5-11e9-92a9-89a0363e40cc  Duration: 853.44 ms  Billed Duration: 900 ms  Memory Size: 256 MB  Max Memory Used: 22 MB
```

## Extending the Function

Ok, let's do something a little more interesting with the function. The input will be a JSON object containing two integers and a string containing an `operation` which will be `add`, `subtract`, `multiply`, or `divide`. The function will take the two integers and perform the operation upon them, returning the result. The JSON will passed to the function will be like:
```json
{
    "value1": 100,
    "value2": 10,
    "operation": "divide"
}
```

Firstly we create an enum to represent the operation:
```c#
public enum Operator
{
    Add,
    Subtract,
    Multiply,
    Divide
}
```

Then we can create a data-class for the JSON to be deserialised into:
```c#
public class InputData
{
    public int Value1 { get; set; }
    public int Value2 { get; set; }
    public Operator Operation { get; set; }
}
```

Now we can update our Lambda handler to process the incoming request, like so:

```c#
    public class Function
    {
        public int FunctionHandler(InputData input)
        {
            switch(input.Operation)
            {
                case Operator.Add:
                    return input.Value1 + input.Value2;
                case Operator.Subtract:
                    return input.Value1 - input.Value2;
                case Operator.Multiply:
                    return input.Value1 * input.Value2;
                case Operator.Divide:
                    return input.Value1 / input.Value2;
                default:
                    throw new Exception("Unknown operator");
            }
        }
    }
```

The signature of the `FunctionHandler` method has been changed to accept an `InputData` object which it uses to perform the calculation. The Lambda service deserialises the JSON object into the `InputData` using the serialiser specified earlier by the line:

```c#
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
```

Once complete, you deploy the code to Lambda by executing:

```
dotnet lambda deploy-function
```

To test the function I created a file `add.json` with content:

```json
{
    "value1": 100,
    "value2": 10,
    "operation": "add"
}
```
And then executed by running
```
dotnet lambda invoke-function --payload .\add.json
```
Then repeated with similar files `subtract.json`, `multiply.json` and `divide.json`.

## Conclusion
Here I have shown the very basics of using AWS Lambda with C# and .NET Core. This acts as a foundation piece that gets you up and running and in future posts I hope to show a few more interesting things you can do with Lambda.