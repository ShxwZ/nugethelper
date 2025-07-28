using System;
using System.Collections.Generic;

namespace BvNugetPreviewGenerator.UI.Models
{
    /// <summary>
    /// State model for package generation progress
    /// </summary>
    public class PackageGenerationState
    {
        public bool IsInProgress { get; set; }
        public int ProgressPercentage { get; set; }
        public string ProgressMessage { get; set; } = string.Empty;
        public string LogContent { get; set; } = string.Empty;
        public string PackagesLeftMessage { get; set; } = string.Empty;
        public PackageGenerationResult Result { get; set; }
        public bool CanCancel { get; set; } = true;
        public bool CanClose { get; set; }
    }

    /// <summary>
    /// Result of package generation operation
    /// </summary>
    public class PackageGenerationResult
    {
        public PackageGenerationResultType Type { get; set; }
        public string Message { get; set; } = string.Empty;
        public Exception Exception { get; set; }

        public static PackageGenerationResult Success(string message = "Package generation completed successfully")
        {
            return new PackageGenerationResult { Type = PackageGenerationResultType.Success, Message = message };
        }

        public static PackageGenerationResult ExpectedFailure(string message, Exception ex = null)
        {
            return new PackageGenerationResult { Type = PackageGenerationResultType.ExpectedFailure, Message = message, Exception = ex };
        }

        public static PackageGenerationResult UnexpectedFailure(Exception ex)
        {
            return new PackageGenerationResult { Type = PackageGenerationResultType.UnexpectedFailure, Message = ex.Message, Exception = ex };
        }
    }

    public enum PackageGenerationResultType
    {
        Success,
        ExpectedFailure,
        UnexpectedFailure
    }

    /// <summary>
    /// Configuration for package generation operation
    /// </summary>
    public class PackageGenerationConfig
    {
        public IEnumerable<string> ProjectPaths { get; set; }
        public string LocalRepoPath { get; set; }
        public string SolutionPath { get; set; }
        public string BuildConfiguration { get; set; }
        public bool Parallel { get; set; }
        public bool AsSolution { get; set; }
        public int MaxDegreeOfParallelism { get; set; }
        public bool ClearLocalRepositoryBeforeBuild { get; set; }
        public bool CleanBeforeBuild { get; set; }
    }

    /// <summary>
    /// Model for package generation configuration properties
    /// Follows MVVM/MVP pattern for better separation of concerns
    /// </summary>
    public class PackageGenerationFormModel
    {
        public IEnumerable<string> ProjectPaths { get; set; }
        public string LocalRepoPath { get; set; } = string.Empty;
        public string SolutionPath { get; set; } = string.Empty;
        public string BuildConfiguration { get; set; } = "Debug";
        public bool Parallel { get; set; } = true;
        public bool AsSolution { get; set; } = false;
        public int MaxDegreeOfParallelism { get; set; } = 4;
        public bool ClearLocalRepositoryBeforeBuild { get; set; } = true;
        public bool CleanBeforeBuild { get; set; } = true;

        /// <summary>
        /// Converts this model to PackageGenerationConfig for presenter consumption
        /// </summary>
        public PackageGenerationConfig ToConfig()
        {
            return new PackageGenerationConfig
            {
                ProjectPaths = ProjectPaths,
                LocalRepoPath = LocalRepoPath,
                SolutionPath = SolutionPath,
                BuildConfiguration = BuildConfiguration,
                Parallel = Parallel,
                AsSolution = AsSolution,
                MaxDegreeOfParallelism = MaxDegreeOfParallelism,
                ClearLocalRepositoryBeforeBuild = ClearLocalRepositoryBeforeBuild,
                CleanBeforeBuild = CleanBeforeBuild
            };
        }

        /// <summary>
        /// Creates a model from PackageGenerationConfig
        /// </summary>
        public static PackageGenerationFormModel FromConfig(PackageGenerationConfig config)
        {
            return new PackageGenerationFormModel
            {
                ProjectPaths = config.ProjectPaths,
                LocalRepoPath = config.LocalRepoPath,
                SolutionPath = config.SolutionPath,
                BuildConfiguration = config.BuildConfiguration,
                Parallel = config.Parallel,
                AsSolution = config.AsSolution,
                MaxDegreeOfParallelism = config.MaxDegreeOfParallelism,
                ClearLocalRepositoryBeforeBuild = config.ClearLocalRepositoryBeforeBuild,
                CleanBeforeBuild = config.CleanBeforeBuild
            };
        }
    }
}