# DRN-Project
[![master](https://github.com/duranserkan/DRN-Project/actions/workflows/master.yml/badge.svg?branch=master)](https://github.com/duranserkan/DRN-Project/actions/workflows/master.yml)
[![develop](https://github.com/duranserkan/DRN-Project/actions/workflows/develop.yml/badge.svg?branch=develop)](https://github.com/duranserkan/DRN-Project/actions/workflows/develop.yml)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=duranserkan_DRN-Project&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=duranserkan_DRN-Project)


[![Security Rating](https://sonarcloud.io/api/project_badges/measure?project=duranserkan_DRN-Project&metric=security_rating)](https://sonarcloud.io/summary/new_code?id=duranserkan_DRN-Project)
[![Maintainability Rating](https://sonarcloud.io/api/project_badges/measure?project=duranserkan_DRN-Project&metric=sqale_rating)](https://sonarcloud.io/summary/new_code?id=duranserkan_DRN-Project)
[![Reliability Rating](https://sonarcloud.io/api/project_badges/measure?project=duranserkan_DRN-Project&metric=reliability_rating)](https://sonarcloud.io/summary/new_code?id=duranserkan_DRN-Project)
[![Vulnerabilities](https://sonarcloud.io/api/project_badges/measure?project=duranserkan_DRN-Project&metric=vulnerabilities)](https://sonarcloud.io/summary/new_code?id=duranserkan_DRN-Project)
[![Bugs](https://sonarcloud.io/api/project_badges/measure?project=duranserkan_DRN-Project&metric=bugs)](https://sonarcloud.io/summary/new_code?id=duranserkan_DRN-Project)
[![Lines of Code](https://sonarcloud.io/api/project_badges/measure?project=duranserkan_DRN-Project&metric=ncloc)](https://sonarcloud.io/summary/new_code?id=duranserkan_DRN-Project)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=duranserkan_DRN-Project&metric=coverage)](https://sonarcloud.io/summary/new_code?id=duranserkan_DRN-Project)

[![Semantic Versioning](https://img.shields.io/badge/semver-2.0.0-3D9FE0.svg)](https://semver.org/)
[![license](https://img.shields.io/github/license/duranserkan/DRN-Project?color=blue)](https://github.com/duranserkan/DRN-Project/blob/master/LICENSE)
[![wiki](https://img.shields.io/badge/Doc-Wiki-brightgreen)](https://github.com/duranserkan/DRN-Project/wiki)
[![wiki](https://img.shields.io/badge/Doc-Roadmap-yellowgreen)](https://github.com/duranserkan/DRN-Project/blob/master/ROADMAP.md)
[![wiki](https://img.shields.io/badge/Doc-Security-gold)](https://github.com/duranserkan/DRN-Project/blob/master/SECURITY.md)
[![GitHub Stars](https://img.shields.io/github/stars/duranserkan/DRN-Project?label=github%20stars)](https://github.com/duranserkan/DRN-Project/stargazers/)
[![All Contributors](https://img.shields.io/github/contributors/duranserkan/DRN-Project.svg)](https://GitHub.com/duranserkan/DRN-Project/graphs/contributors)
[![Activity](https://img.shields.io/github/commit-activity/m/duranserkan/DRN-Project)](https://github.com/duranserkan/DRN-Project/graphs/commit-activity)

[![Nuget](https://img.shields.io/nuget/dt/DRN.Framework.Testing?logo=Nuget&label=DRN.Framework.Testing)](https://nuget.org/packages/DRN.Framework.Testing/)

[![General badge](https://img.shields.io/badge/LinkedIn-0077B5?style=for-the-badge&logo=linkedin&logoColor=white.svg)](https://www.linkedin.com/in/duranserkan/)
[![General badge](https://img.shields.io/badge/Medium-12100E?style=for-the-badge&logo=medium&logoColor=white)](https://duranserkan.medium.com)


Distributed Reliable .Net project aims to provide somewhat opinionated design and out of the box solutions to enterprise application development. 
Expected result is spending less time on wiring while getting better maintainability and observability.
The project benefits from the best practices, open source solutions and personal production experience.
This project is about managing software complexity, architecting good solutions and a promise to deliver a good software.

## About Project
DRN Project is not another framework that will `bite the dust.` It is more than a simple framework. It is a distilled knowledge that contains:
- [X] A nexus app to manage microservices
- [X] Management best practices
- [X] Engineering manifest
- [X] Reference documents
- [X] A beautiful framework to work with.


This project is result of a productive and curious mindset that respects good solutions and best practices of others while enjoys from creating its own.
It is not about coding. It is about process of creating and enhancing good things. I expect it to be ready for general purpose usage within 6 months. 
Detailed documentation will be added in the mean time.


### Dotnet Solution Structure
This solution consists of 6 parts that are being developed with Jetbrains Rider on macOS with arm-based M2 chip. However I expect it to work on any modern machine since it is a cross platform solution
1. **Docker Folder:** It contains dockerfile and compose definitions for dependencies. They just works and can be used for other solutions.
2. **Docs:** It contains project documents. I am planning to support this docs with articles and a youtube playlist.
3. **Items:** It contains file that does not belong anywhere else such as .gitignore, .dockerignore and .github workflows
4. **Src:** It contains 3 folders that define 3 different **DRN** domain.
   * **Nexus:** Nexus app connects all services and apis developed with **DRN.Framework**.
     `The one microservice to rule them all.`
      * It is an unified web app that contains nexus api and background services.
      * It will have following features as a start:
        - [ ] **Configuration Management:** Nexus can provide configurations to connected microservices
        - [ ] **Migration Management:** Nexus can manage microservice database migrations which includes schema management and sharding
        - [ ] **Service Discovery:** Nexus discloses microservice network
        - [ ] **Observability, Documentation and Monitoring Hub:** Nexus provides and visualizes workflow graph and information regarding to them.
   * **Framework:** DRN.Framework source codes belongs to generalized solutions that can be used within any dotnet project as nuget packages. 
   * **Sample:** Nexus connectable sample app demonstrates **DRN.Framework** usage. It is used for testing, presentation and documentation purposes.
5. **Test:** It contains all the unit, integration and performance tests.
6. **docker-compose:** Global docker-compose file that binds Nexus, Sample microservice and their dependencies.

## About Management
### Task Management
* Always make a Todo List
* Prioritize tasks - Use your insights or MoSCoW prioritization etc...
* Warm up - start with one easy task then focus on hard one.
* Know your options 4D+E - Do it, drop it, delegate it, defer it or escalate.
> 4D comes from David Allen's 4d model. +E is my personal addition to it. Escalation is your friend. Inform others and ask their opinions whenever you are not sure what to do.
### Time Management
* Small things add up then become big thing. Do it while they are small and avoid stress.
* Don't micro manage delegate whenever possible.
  * Delegation requires shared understanding. It is not fire and forget process. Guide and track results when needed
* Don't over optimize. Good enough is better than excellent.
### Security Management
* Security is always your most important requirement.
* Always be defensive and secure by default
* Prefer whitelisting over blacklisting. Blacklists can be penetrated.
### Quality Management
* Follow best practices and solutions. Don't reinvent the wheel
* Denormalize when needed. Understand how it works. Don't follow rules blindly. 
  * You can accomplish things that considered not possible if you understanding how things works.
  * Understand concepts and how they relate with each other.
* Prefer quality over quantity.
* Always test before deliver.
* Measure performance
### Risk Management
* No risk no reward
* Take managed risk and risk what you are willing to lose
* Sandbox risks and threats
* Improve your odds
* Make reasonable assumptions
### Personal Management
* Focus on good thoughts
* Be persistent and pay attention to details
* Be good to yourself
* Don't compromise your integrity
* You can only expand yourself as your environment allows. Don't hesitate to change it when necessary 
* You are not a tree. You can always walk away

## About Architecture and Microservices
* [Chris Patterson's Great Article - Software Architect for Life](http://blog.phatboyg.com/2017/03/08/software-architect-for-life.html)
* [DDD Oriented Microservice](https://docs.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/ddd-oriented-microservice)
* [Strategic Domain Driven Design](https://vaadin.com/blog/ddd-part-1-strategic-domain-driven-design)
* [Tactical Domain Driven Design](https://vaadin.com/blog/ddd-part-2-tactical-domain-driven-design)
* [DDD and Hexagonal Architecture](https://vaadin.com/blog/ddd-part-3-domain-driven-design-and-the-hexagonal-architecture)

## Duran's Engineering Manifest
Today, I hereby declare that as an engineer,
* I am passionate about designing, developing, delivering high quality and cost-effective software.
* I acquire new skills with continuous learning.
* I am involved in every stage of the Software Development Life Cycle from planning to implementation.
* I am a conceptual thinker.
* I do not hesitate to take initiative.
* I support all standardization activities that make my work easier and understandable.
* I force myself to improve myself and those around me.
* I won't be trapped in habits and comfort zone.
* I pay attention to details and deal with problems with a holistic approach.
* I care about quality in my work.
* I do my best not to leave technical debt. If I have to leave technical debt, I will remove the technical debt as soon as possible.
* I manage time and stress under pressure, and I am not afraid to compromise and ask for help.
* I will always keep communication channels open.
* I will always give constructive and positive feedback whenever possible and appropriate.
* I will always be open to constructive and positive feedback.
* I will always be conscientious, transparent and will live by values and goals.