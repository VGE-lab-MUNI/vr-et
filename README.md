# Eye-Tracking in Virtual Reality (vr-et)
This is a companion repository to eye-tracking in 3D/VR research, Masaryk University.

## Table of Contents

* [General info](#general-info)
* [Technologies](#technologies)
* [Setup](#setup)
* [Publications](#publications)
* [Version](#version)
* [License](#license)

## General info
The provided algorithms are to serve as a technology-agnostic framework, to be used with VR head-mounted disply eye-tracking technology (e.g., Tobii XR SDK, Pupil Labs, etc.)

The idea behind the software architecture is as follows:
* Experiment runtime (\experiment), to provide data logging and real-time features
* Evaluation runtime (\verification and \visualization), to fix acquired data, and to see it visualized
* External applications, scripts, eye-tracking data filtering

This is meant as two separate runtimes (Unity), with etc. scripting on the side
This is to follow the proposed software architecture (see image):

## Technologies
C# (Unity API)
Processing (Java, external scripting)

## Setup
Download the files into your Unity project (Unity 2018.4 and newer, render pipeline doesn't matter).

For data logging, set up PathScript.
For evaluation, use CSVReader to load data, and then \verification and \visualization to do the rest.

For further instructions, read each script's documentation.

## Publications
Eye-Tracking in Interactive Virtual Environments: Implementa-tion and Evaluation (under review)

## Version
Major release info will go here.
Legacy compatibility with initial files/release needs to be maintained, should major changes commence.

## License
Creative Commons CC BY-NC

This license lets others remix, adapt, and build upon your work non-commercially, and although their new works must also acknowledge you and be non-commercial, they don’t have to license their derivative works on the same terms.
