using Xunit;
using SmartHadithTree.Application.Services;
using SmartHadithTree.Application.DTOs;
using System;
using System.Collections.Generic;

namespace SmartHadithTree.Tests.Application;

public class TaqwiyahServiceTests
{
    [Fact]
    public void CalculateTreeStrength_ShouldUpgradeTwoWeakChainsToHasan()
    {
        // Arrange
        var service = new TaqwiyahService();
        var tree = new ComparativeTreeResponseDto
        {
            Nodes = new List<ComparativeIsnadNodeDto>
            {
                new ComparativeIsnadNodeDto { Id = Guid.NewGuid(), StepOrder = 1, GradeEn = "weak", ParentNodeId = null },
                new ComparativeIsnadNodeDto { Id = Guid.NewGuid(), StepOrder = 1, GradeEn = "weak", ParentNodeId = null }
            }
        };

        // Act
        service.CalculateTreeStrength(tree);

        // Assert
        Assert.Equal("حسن لغيره", tree.CalculatedGrade);
    }

    [Fact]
    public void CalculateTreeStrength_ShouldNotUpgradeFabricator()
    {
        // Arrange
        var service = new TaqwiyahService();
        var tree = new ComparativeTreeResponseDto
        {
            Nodes = new List<ComparativeIsnadNodeDto>
            {
                new ComparativeIsnadNodeDto { Id = Guid.NewGuid(), StepOrder = 1, GradeEn = "fabricator", ParentNodeId = null },
                new ComparativeIsnadNodeDto { Id = Guid.NewGuid(), StepOrder = 1, GradeEn = "fabricator", ParentNodeId = null }
            }
        };

        // Act
        service.CalculateTreeStrength(tree);

        // Assert
        Assert.Equal("موضوع / متروك", tree.CalculatedGrade);
    }
}
