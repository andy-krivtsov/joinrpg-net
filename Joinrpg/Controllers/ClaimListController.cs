﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using JetBrains.Annotations;
using JoinRpg.Data.Interfaces;
using JoinRpg.DataModel;
using JoinRpg.Domain;
using JoinRpg.Services.Interfaces;
using JoinRpg.Web.Helpers;
using JoinRpg.Web.Models;

namespace JoinRpg.Web.Controllers
{
  public class ClaimListController : Common.ControllerGameBase
  {
    private IClaimsRepository ClaimsRepository { get; }

    public ClaimListController(ApplicationUserManager userManager, IProjectRepository projectRepository,
      IProjectService projectService, IExportDataService exportDataService, IClaimsRepository claimsRepository)
      : base(userManager, projectRepository, projectService, exportDataService)
    {
      ClaimsRepository = claimsRepository;
    }

    #region implementation

    //TODO Merge to MasterClaimList
    private async Task<ActionResult> ShowProblems(int projectId, string export, ICollection<Claim> claims)
    {
      var error = await AsMaster(claims, projectId);
      if (error != null)
        return error;

      var viewModel =
        claims.Select(c => new ClaimListItemViewModel(c, CurrentUserId).AddProblems(c.GetProblems()))
          .Where(vm => vm.Problems.Any(p => p.Severity >= ProblemSeverity.Warning))
          .ToList();

      ViewBag.ClaimIds = viewModel.Select(c => c.ClaimId).ToArray();
      ViewBag.Title = "Проблемные заявки";
      ViewBag.HideProjectColumn = true;
      ViewBag.MasterAccessColumn = true;

      var exportType = GetExportTypeByName(export);

      if (exportType == null)
      {
        return View("Index", viewModel);
      }

      return await Export(viewModel, "problem-claims", exportType.Value);
    }

    private async Task<ActionResult> MasterClaimList(int projectId, Func<Claim, bool> predicate, string export,
      string title, [AspMvcView] string viewName = "Index")
    {
      var claims = (await ClaimsRepository.GetClaims(projectId)).Where(predicate).ToList();

      var error = await AsMaster(claims, projectId);
      if (error != null) return error;

      var viewModel =
        claims.Select(claim => new ClaimListItemViewModel(claim, CurrentUserId).AddProblems(claim.GetProblems()))
          .ToList();
      
      var exportType = GetExportTypeByName(export);

      if (exportType == null)
      {
        ViewBag.ClaimIds = viewModel.Select(c => c.ClaimId).ToArray();
        ViewBag.HideProjectColumn = true;
        ViewBag.MasterAccessColumn = true;
        ViewBag.Title = title;
        return View(viewName, viewModel);
      }
      else
      {
        var project = await GetProjectFromList(projectId, claims);

        return await ExportWithCustomFronend(viewModel, title, exportType.Value, new ClaimListItemViewModelExporter(project.ProjectFields), project.ProjectName);
      }
    }
    #endregion

    [HttpGet, Authorize]
    public Task<ActionResult> ForPlayer(int projectId, int userId, string export)
      => MasterClaimList(projectId, cl => cl.IsActive & cl.PlayerUserId == userId, export, "Заявки на игроке");

    [HttpGet, Authorize]
    public Task<ActionResult> ListForGroupDirect(int projectId, int characterGroupId, string export)
    {
      ViewBag.CharacterGroupId = characterGroupId;
      return MasterClaimList(projectId, cl => cl.IsActive && cl.CharacterGroupId == characterGroupId, export,
        "Заявки в группу (напрямую)", "ListForGroupDirect");
    }

    [HttpGet, Authorize]
    public Task<ActionResult> ListForGroup(int projectId, int characterGroupId, string export)
    {
      ViewBag.CharacterGroupId = characterGroupId;
      return MasterClaimList(projectId, cl => cl.IsActive && cl.IsPartOfGroup(characterGroupId), export,
        "Заявки в группу (все)", "ListForGroup");
    }

    [HttpGet, Authorize]
    public Task<ActionResult> DiscussingForGroup(int projectId, int characterGroupId, string export)
    {
      ViewBag.CharacterGroupId = characterGroupId;
      return MasterClaimList(projectId, cl => cl.IsInDiscussion && cl.IsPartOfGroup(characterGroupId), export,
        "Обсуждаемые заявки в группу (все)", "DiscussingForGroup");
    }

    [HttpGet, Authorize]
    public Task<ActionResult> DiscussingForGroupDirect(int projectId, int characterGroupId, string export)
    {
      ViewBag.CharacterGroupId = characterGroupId;
      return MasterClaimList(projectId, cl => cl.IsInDiscussion && cl.CharacterGroupId == characterGroupId, export,
        "Обсуждаемые заявки в группу (напрямую)", "DiscussingForGroupDirect");
    }

    [HttpGet, Authorize]
    public Task<ActionResult> ResponsibleDiscussing(int projectid, int responsibleMasterId, string export)
      =>
        MasterClaimList(projectid, claim => claim.ResponsibleMasterUserId == responsibleMasterId && claim.IsInDiscussion,
          export, "Обсуждаемые заявки на мастере");

    [HttpGet, Authorize]
    public Task<ActionResult> ResponsibleOnHold(int projectid, int responsiblemasterid, string export)
      =>
        MasterClaimList(projectid,
          claim => claim.ResponsibleMasterUserId == responsiblemasterid && claim.ClaimStatus == Claim.Status.OnHold,
          export, "Лист ожидания на мастере");


    [HttpGet, Authorize]
    public Task<ActionResult> ActiveList(int projectid, string export)
      => MasterClaimList(projectid, claim => claim.IsActive, export, "Активные заявки");

    [HttpGet, Authorize]
    public Task<ActionResult> DeclinedList(int projectid, string export)
      => MasterClaimList(projectid, claim => !claim.IsActive, export, "Отклоненные/отозванные заявки");

    [HttpGet, Authorize]
    public Task<ActionResult> Discussing(int projectid, string export)
      => MasterClaimList(projectid, claim => claim.IsInDiscussion, export, "Обсуждаемые заявки");

    [HttpGet, Authorize]
    public Task<ActionResult> OnHoldList(int projectid, string export)
      => MasterClaimList(projectid, claim => claim.ClaimStatus == Claim.Status.OnHold, export, "Лист ожидания");

    [HttpGet, Authorize]
    public Task<ActionResult> WaitingForFee(int projectid, string export)
      =>
        MasterClaimList(projectid, claim => claim.IsApproved && !claim.ClaimPaidInFull(), export,
          "Неоплаченные принятые заявки");

    [HttpGet, Authorize]
    public Task<ActionResult> Responsible(int projectid, int responsibleMasterId, string export)
      =>
        MasterClaimList(projectid, claim => claim.ResponsibleMasterUserId == responsibleMasterId && claim.IsActive,
          export, "Заявки на мастере", "Responsible");

    [HttpGet, Authorize]
    public ActionResult My() => View(GetCurrentUser().Claims.Select((claim, currentUserId) => new ClaimListItemViewModel(claim, currentUserId)));

    [HttpGet, Authorize]
    public async Task<ActionResult> Problems(int projectId, string export)
    {
      return await ShowProblems(projectId, export, await ClaimsRepository.GetClaims(projectId));
    }

    [HttpGet, Authorize]
    public async Task<ActionResult> ResponsibleProblems(int projectId, int responsibleMasterId, string export)
    {
      return
        await
          ShowProblems(projectId, export,
            await ClaimsRepository.GetActiveClaimsForMaster(projectId, responsibleMasterId));
    }

   
  }
}