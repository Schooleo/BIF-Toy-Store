# BIF-Toy-Store POS

[![CI Pipeline](https://github.com/Schooleo/BIF-Toy-Store/actions/workflows/CI.yaml/badge.svg)](https://github.com/Schooleo/BIF-Toy-Store/actions/workflows/CI.yaml)
[![CD Pipeline](https://github.com/Schooleo/BIF-Toy-Store/actions/workflows/CD.yaml/badge.svg)](https://github.com/Schooleo/BIF-Toy-Store/actions/workflows/CD.yaml)

A Point of Sale (POS) application for a toy store, built as a modern desktop application using **WinUI 3** and **C#** (.NET). The project follows a structured **Clean Architecture** and the **MVVM** design pattern, ensuring separation of concerns and maintainability.

## Architecture & Project Structure

The solution (`BIF-Toy-Store.slnx`) consists of four main projects located in the `src/` directory:

- **`BIF.ToyStore.Core`**: Contains the central domain models (e.g., `Product`, `Order`, `Customer`, `User`, `Category`), application interfaces, enumerations, and global settings.
- **`BIF.ToyStore.Infrastructure`**: Implements the repository pattern, data access, and external services. It notably includes a **GraphQL** client integration layer to communicate with backend APIs.
- **`BIF.ToyStore.ViewModels`**: Houses the application's presentation logic, separating the View from the Model. It contains the view models for individual pages as well as base implementations.
- **`BIF.ToyStore.WinUI`**: The frontend presentation layer built using the Windows UI Library (WinUI 3). It contains the XAML views (such as the `LoginPage`), styles, assets, and application entry point.

## Tech Stack

- **C# / .NET**
- **WinUI 3** (Windows App SDK)
- **MVVM** (Model-View-ViewModel)
- **GraphQL**
- **Clean Architecture**

## Automated Pipelines & Testing

This repository utilizes **GitHub Actions** to automate health checks and deployment:

- **CI Pipeline (`CI.yaml`)**: Triggers on pushes and Pull Requests to the `main` and `dev` branches. It restores dependencies, builds the solution, runs automated **xUnit** / **Moq** tests (such as `LoginViewModelTests`), and uploads the `.trx` test results as artifacts.
- **CD Pipeline (`CD.yaml`)**: Manually triggered via `workflow_dispatch`. It protects the codebase using **Obfuscar**, publishes the app, and produces an MSIX installer payload ready for deployment.

## Getting Started

1. Open the solution (`src/BIF-Toy-Store.slnx`) in Visual Studio 2022. Ensure you have the Windows App SDK and WinUI workloads installed.
2. Set the `BIF.ToyStore.WinUI` project as the Startup Project.
3. Build and Run the application to launch the Toy Store POS interface.

---

### Contributors

<table>
  <tr>
    <td align="center">
      <a href="https://github.com/KwanTheAsian">
        <img src="https://avatars.githubusercontent.com/KwanTheAsian" width="100px;" alt="KwanTheAsian"/><br />
        <sub><b>23127020 - Biện Xuân An</b></sub>
      </a><br />
      📝 Business Analyst / Developer
    </td>
    <td align="center">
      <a href="https://github.com/PaoPao1406">
        <img src="https://avatars.githubusercontent.com/PaoPao1406" width="100px;" alt="PaoPao1406"/><br />
        <sub><b>23127025 - Đoàn Lê Gia Bảo</b></sub>
      </a><br />
      🎨 UI/UX Designer / Developer
    </td>
    <td align="center">
      <a href="https://github.com/VNQuy94">
        <img src="https://avatars.githubusercontent.com/VNQuy94" width="100px;" alt="VNQuy94"/><br />
        <sub><b>23127114 - Văn Ngọc Quý</b></sub>
      </a><br />
      ⚙️ System Designer / Developer
    </td>
    <td align="center">
      <a href="https://github.com/Schooleo">
        <img src="https://avatars.githubusercontent.com/Schooleo" width="100px;" alt="Schooleo"/><br />
        <sub><b>23127136 - Lê Nguyễn Nhật Trường</b></sub>
      </a><br />
      💻 Project Manager / Developer
    </td>
  </tr>
</table>
