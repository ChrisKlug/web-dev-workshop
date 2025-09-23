# Web Dev Workshop

This repo contains the instructions for running a 2-day web development workshop.

## The goal

The end goal during this workshop is to build a solution that looks like the following

![Architecture Overview](resources/architecture-overview.png)

## Workshop outline

The 2 days include a combination of presentations and labs, and breaks of course. The general ourline is as follows

### Day 1

- Intro to the solution and what we are building
- Presentation: Getting started with Aspire
- [Lab 1: Setting up Aspire](./labs/lab1.md)
- Presentation: What is YARP (Yet Another Reverse Proxy)
- [Lab 2: Setting up YARP](./labs/lab2.md)
- [Lab 3: Adding a SQL Server database](./labs/lab3.md)
- [Lab 4: Creating a Products Service](./labs/lab4.md)
- Presentation: FastEndpoints - Another API endpoint option
- [Lab 5: Implementing the Products API using FastEndpoints](./labs/lab5.md)
- Presentation: Testing APIs
- [Lab 6: Integration Testing the Products API](./labs/lab6.md)
- [Lab 7: Clean up the Integration Testing](./labs/lab7.md)
- [Lab 8: Testing the Product Endpoint](./labs/lab8.md)
- Presentation: The benefits of SDK:s over APIs
- [Lab 9: Creating an SDK for the Products API](./labs/lab9.md)
- [Lab 10: Providing the UI with Products](./labs/lab10.md)

### Day 2
- Recap and goal for the day
- Presentation: Project Orleans - The very quick primer
- [Lab 11: Creating an Orleans-based Shopping Cart](./labs/lab11.md)
- [Lab 12: Persisting the Shopping Cart](./labs/lab12.md)
- [Lab 13: Testing the Shopping Cart Endpoints](./labs/lab13.md)
- Presentation: ASP.NET Core Authentication
- [Lab 14: Adding a IdentityServer](./labs/lab14.md)
- [Lab 15: Adding User Authentication](./labs/lab15.md)
- [Lab 16: Testing with Authentication](./labs/lab16.md)
- Presentation: gRPC?
- [Lab 17: Creating a gRPC-based Orders Service](./labs/lab17.md)
- [Lab 18: Testing gRPC Services](./labs/lab18.md)
- [Lab 19: Consuming gRPC Services](./labs/lab19.md)
- Presentation: Introduction to OpenTelemetry
- [Lab 20: Adding Custom Data to the OTEL Traces](./labs/lab20.md)
- Presentation: Optional: The Outbox Pattern and EF Core Interceptors
- [Lab 21: Optional: Outbox Pattern using EF Core Interceptors](./labs/lab21.md)

## Source Code

Well, you are already in the repo that contains everything you need. And it does include a fully implemented solution in the [src directory](./src). 

The source code can also be "rewound" to the state as it should look after each lab. Just check out the branch with the name __Lab-XX__, where __XX__ is the lab number.

## Questions?

If you are in the workshop, just let Chris know that you have a question. If you are not currently attending the workshop, you can contact Chris on Twitter, where his handle is [@ZeroKoll](https://twitter.com/ZeroKoll), or through mail at chris(a)59north.com.