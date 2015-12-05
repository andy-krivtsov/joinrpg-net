﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using JoinRpg.DataModel;
using JoinRpg.Domain;
using JoinRpg.Helpers;
using JoinRpg.Services.Interfaces;
using Mailgun.Core.Messages;
using Mailgun.Messages;
using Mailgun.Service;
using Newtonsoft.Json.Linq;

namespace JoinRpg.Services.Email
{
  [UsedImplicitly]
  public class EmailServiceImpl : IEmailService
  {
    private const string JoinRpgTeam = "Команда JoinRpg.Ru";
    private readonly string _apiDomain;

    private readonly Recipient _joinRpgSender;

    private readonly Lazy<MessageService> _lazyService;
    private MessageService MessageService => _lazyService.Value;

    private readonly IHtmlService _htmlService;
    private readonly IUriService _uriService;

    private Task Send(IMessage message)
    {
      return MessageService.SendMessageAsync(_apiDomain, message);
    }

    public EmailServiceImpl(IHtmlService htmlService, IUriService uriService, IMailGunConfig config)
    {
      _apiDomain = config.ApiDomain;
      _htmlService = htmlService;
      _joinRpgSender = new Recipient()
      {
        DisplayName = JoinRpgTeam,
        Email = "support@" + config.ApiDomain
      };
      _uriService = uriService;
      _lazyService = new Lazy<MessageService>(() => new MessageService(config.ApiKey));
    }

    private Task SendEmail(User recepient, string subject, string text, Recipient sender)
    {
      return SendEmail(new[] {recepient}, subject, text, sender);
    }

    private async Task SendEmail(ICollection<User> recepients, string subject, string text, Recipient sender)
    {
      if (!recepients.Any())
      {
        return;
      }
      var html = _htmlService.MarkdownToHtml(new MarkdownString(text));
      var message = new MessageBuilder().AddUsers(recepients)
        .SetSubject(subject)
        .SetFromAddress(new Recipient() {DisplayName = sender.DisplayName, Email = _joinRpgSender.Email})
        .SetReplyToAddress(sender)
        .SetTextBody(text)
        .SetHtmlBody(html)
        .GetMessage();
      message.RecipientVariables =
        JObject.Parse("{" +string.Join(", ", recepients.Select(r => $"\"{r.Email}\":{{\"name\":\"{r.DisplayName}\"}}")) + "}");
      await Send(message);
    }

    #region Account emails
    public Task Email(RemindPasswordEmail email)
    {
      return SendEmail(email.Recepient, "Восстановление пароля на JoinRpg.Ru",
        $@"Добрый день, %recipient.name%, 

вы (или кто-то, выдающий себя за вас) запросил восстановление пароля на сайте JoinRpg.Ru. 
Если это вы, кликните <a href=""{
          email.CallbackUrl
          }"">вот по этой ссылке</a>, и мы восстановим вам пароль. 
Если вдруг вам пришло такое письмо, а вы не просили восстанавливать пароль, ничего страшного! Просто проигнорируйте его.

--
{JoinRpgTeam}", _joinRpgSender);
    }

    public Task Email(ConfirmEmail email)
    {
      return SendEmail(email.Recepient, "Регистрация на JoinRpg.Ru",
        $@"Здравствуйте, и добро пожаловать на joinrpg.ru!

Пожалуйста, подтвердите свой аккаунт, кликнув <a href=""{
            email.CallbackUrl
            }"">вот по этой ссылке</a>.

Это необходимо для того, чтобы мастера игр, на которые вы заявитесь, могли надежно связываться с вами.

Если вдруг вам пришло такое письмо, а вы нигде не регистрировались, ничего страшного! Просто проигнорируйте его.

--
{JoinRpgTeam}", _joinRpgSender);
    }
    #endregion

    private async Task SendClaimEmail(ClaimEmailModel model, string actionName, string text = "")
    {
      if (model.ProjectName.Trim().StartsWith("NOEMAIL"))
      {
        return;
      }
      var recepients = model.Recepients.Except(new [] {model.Initiator}).ToList();
      if (!recepients.Any())
      {
        return;
      }

      await SendEmail(recepients, $"{model.ProjectName}: {model.Claim.Name}, игрок {model.GetPlayerName()}",
        $@"Добрый день, %recipient.name%!
Заявка {model.Claim.Name} игрока {model.Claim.Player.DisplayName} {actionName} {model.GetInitiatorString()}
{text}

{model.Text.Contents}

{model.Initiator.DisplayName}

Чтобы ответить на комментарий, перейдите на страницу заявки: {_uriService.Get(model.Claim)}
", model.Initiator.ToRecipient());
    }

    public Task Email(AddCommentEmail model) => SendClaimEmail(model, "откомментирована");

    public Task Email(ApproveByMasterEmail model) => SendClaimEmail(model, "одобрена");

    public Task Email(DeclineByMasterEmail model) => SendClaimEmail(model, "отклонена");

    public Task Email(DeclineByPlayerEmail model) => SendClaimEmail(model, "отозвана");

    public Task Email(NewClaimEmail model) => SendClaimEmail(model, "подана");

    public Task Email(RestoreByMasterEmail model) => SendClaimEmail(model, "восстановлена");

    public Task Email(MoveByMasterEmail model)
      =>
        SendClaimEmail(model, "изменена",
          $@"Заявка перенесена {model.GetInitiatorString()} на новую роль «{model.Claim.GetTarget().Name}».");

    public Task Email(FinanceOperationEmail model)
    {
      var message = "";

      if (model.FeeChange != 0)
      {
        message += $"\nИзменение взноса: {model.FeeChange}\n";
      }

      if (model.Money > 0)
      {
        message += $"\nОплата денег игроком: {model.Money}\n";
      }

      if (model.Money < 0)
      {
        message += $"\nВозврат денег игроку: {-model.Money}\n";
      }

      return SendClaimEmail(model, "отмечена", message);
    }
  }

  internal static class Exts
  {
    public static IMessageBuilder AddUsers(this IMessageBuilder builder, IEnumerable<User> users)
    {
      foreach (var user in users.WhereNotNull().Distinct())
      {
        builder.AddToRecipient(user.ToRecipient());
      }
      return builder;
    }

    public static Recipient ToRecipient(this User user)
    {
      return new Recipient() {DisplayName = user.DisplayName, Email = user.Email};
    }

    public static string GetInitiatorString(this ClaimEmailModel model)
    {
      switch (model.InitiatorType)
      {
        case ParcipantType.Nobody:
          return "";
        case ParcipantType.Master:
          return $"мастером {model.Initiator.DisplayName}";
        case ParcipantType.Player:
          return "игроком";
        default:
          throw new ArgumentOutOfRangeException(nameof(model.InitiatorType), model.InitiatorType, null);
      }
    }

    public static string GetPlayerName(this ClaimEmailModel model)
    {
      return model.Claim.Player.DisplayName;
    }
  }
}
