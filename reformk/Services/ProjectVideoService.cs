using Reformk.Data;
using Microsoft.EntityFrameworkCore;

namespace Reformk.Services;

public sealed class ProjectVideoService(ApplicationDbContext dbContext)
{
    public async Task<VideoCatalogItem?> FindVideoAsync(string videoId, CancellationToken cancellationToken)
    {
        return await dbContext.Videos
            .Include(video => video.Inputs)
            .Include(video => video.Tasks)
            .Include(video => video.SemanticIndex)
            .FirstOrDefaultAsync(video => video.VideoId == videoId, cancellationToken);
    }

    public void ReplaceVideo(ReformProject project, VideoCatalogItem? video)
    {
        ArgumentNullException.ThrowIfNull(project);

        dbContext.ProjectInputs.RemoveRange(project.Inputs);
        dbContext.ProjectTasks.RemoveRange(project.Tasks);
        project.Inputs.Clear();
        project.Tasks.Clear();
        project.SelectedVideo = video;
        project.SelectedVideoId = video?.VideoId;
        project.WorkDescription = null;

        if (video is null)
        {
            return;
        }

        var sourceInputs = video.Inputs.ToArray();
        var copiedInputs = sourceInputs
            .Select(input => new ProjectInput
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                Kind = input.Kind,
                Name = input.Name,
                Brand = input.Brand,
                Specification = input.Specification,
                Quantity = input.Quantity,
                Unit = input.Unit,
                Category = input.Category,
                Level = input.Level,
                Headcount = input.Headcount,
                EstimatedHours = input.EstimatedHours,
                Notes = input.Notes,
                IsManual = false
            })
            .ToArray();
        var inputsBySourceId = sourceInputs
            .Select((input, index) => (input.SourceId, Copied: copiedInputs[index]))
            .ToDictionary(item => item.SourceId, item => item.Copied, StringComparer.Ordinal);

        dbContext.ProjectInputs.AddRange(copiedInputs);

        foreach (var definition in video.Tasks.OrderBy(task => task.Order))
        {
            var task = new ProjectTask
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                Order = definition.Order,
                Title = definition.Title,
                Description = definition.Description
            };
            foreach (var sourceId in definition.MaterialIds
                         .Concat(definition.EquipmentIds)
                         .Concat(definition.LaborIds)
                         .Distinct(StringComparer.Ordinal))
            {
                if (inputsBySourceId.TryGetValue(sourceId, out var input))
                {
                    task.Inputs.Add(input);
                }
            }

            project.Tasks.Add(task);
            dbContext.ProjectTasks.Add(task);
        }
    }
}
