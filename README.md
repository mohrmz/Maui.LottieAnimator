# MauiLottiePlayer-SkiaSharp

MauiLottiePlayer-SkiaSharp is a .NET MAUI project built to demonstrate an interactive and reliable Lottie animation player using SkiaSharp. The focus of the project is to provide a clean implementation for loading and controlling vector-based Lottie animations in a cross-platform application.

## Project Overview

The project provides a simple and reusable component that can display Lottie animations without visual distortion, regardless of screen size or device orientation. It supports loading any Lottie `.json` file included in the application and offers basic playback functionality such as play, pause, and frame seeking. The rendering is handled through SkiaSharp to ensure smooth, high-quality output.

## Objectives

- Provide an interactive animation viewer for Lottie JSON files.  
- Maintain correct aspect ratio across different devices without stretching or pixel loss.  
- Enable users to move through the animation timeline using a scrub or seek control.  
- Allow multiple animation files to be added and loaded easily within the project.  
- Demonstrate clean structure and organized implementation suitable for integration into other MAUI applications.

## Features

- Playback controls including play and pause.  
- A seek slider for navigating through frames or animation progress.  
- Dynamic loading of different Lottie animations stored in the application.  
- Consistent rendering quality using SkiaSharp’s vector capabilities.  
- Cross-platform compatibility on Android, iOS, Windows, and macOS.

## Technologies Used

- .NET MAUI  
- C#  
- SkiaSharp  
- Lottie-Skia / SkiaSharp.Extended  

## High-Level Structure

- `Controls/` – Custom animation player control.  
- `Views/` – Demonstration pages for displaying and interacting with animations.  
- `Resources/Raw/` – Lottie animation files in JSON format.  
- `README.md` – Documentation describing the purpose and structure of the project.

## Summary

This project serves as a clear and practical example of integrating Lottie animations into a .NET MAUI application using SkiaSharp. It emphasizes controlled playback, accurate rendering, and maintainable structure without relying on external dependencies beyond the essential libraries.
