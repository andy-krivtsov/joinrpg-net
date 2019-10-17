using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http;
using JoinRpg.Data.Interfaces;
using JoinRpg.Domain;
using JoinRpg.Domain.Schedules;
using JoinRpg.Helpers;
using Joinrpg.Markdown;
using JoinRpg.Web.Models.Schedules;
using JoinRpg.Web.XGameApi.Contract.Schedule;
using JoinRpg.WebPortal.Managers.Schedule;

namespace JoinRpg.Web.Controllers.XGameApi
{
    [RoutePrefix("x-game-api/{projectId}/schedule")]
    public class ProjectScheduleController : XGameApiController
    {
        private SchedulePageManager Manager { get; }

        public ProjectScheduleController(IProjectRepository projectRepository, SchedulePageManager manager) : base(projectRepository)
        {
            Manager = manager;
        }

        [HttpGet]
        [Route("all")]
        public async Task<List<ProgramItemInfoApi>> GetSchedule([FromUri]
            int projectId)
        {
            var check = await Manager.CheckScheduleConfiguration();
            if (check.Contains(ScheduleConfigProblemsViewModel.NoAccess))
            {
                throw new HttpResponseException(HttpStatusCode.Forbidden);
            }
            if (check.Any())
            {
                throw new Exception($"Error {check.Select(x => x.ToString()).JoinStrings(" ,")}");
            }

            var project = await ProjectRepository.GetProjectWithFieldsAsync(projectId);
            var characters = await ProjectRepository.GetCharacters(projectId);
            var scheduleBuilder = new ScheduleBuilder(project, characters);
            var result = scheduleBuilder.Build().AllItems.Select(slot =>
                new ProgramItemInfoApi
                {
                    ProgramItemId = slot.ProgramItem.Id,
                    Name = slot.ProgramItem.Name,
                    Authors = slot.ProgramItem.Authors.Select(author =>
                        new AuthorInfoApi
                        {
                            UserId = author.UserId,
                            Name = author.GetDisplayName()
                        }),
                    StartTime = slot.StartTime,
                    EndTime = slot.EndTime,
                    Rooms = slot.Rooms.Select(room => new RoomInfoApi
                    {
                        RoomId = room.Id,
                        Name = room.Name
                    }),
                    Description = slot.ProgramItem.Description.ToHtmlString().ToString()
                }).ToList();
            return result;
        }
    }
}