﻿using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Optional;
using System.Net.Http.Headers;
using Twitch.Net.EventSub.Events;
using Twitch.Net.EventSub.Models;

namespace Twitch.Net.EventSub;

public interface IEventSubService
{
    IEventSubEventHandler Events { get; }
    Task<IActionResult> Handle(HttpRequest request);
    Task<SubscribeResult> Subscribe(SubscribeModel model, string? token = null);
    Task<bool> Unsubscribe(string id, string? token = null);
    Task<Option<RegisteredSubscriptions>> Subscriptions(
        string? status = null,
        string? type = null,
        string? pagination = null,
        string? token = null
        );
}

public interface IEventSubService2
{
    IEventSubEventHandler Events { get; }

    SubscribeCallbackResponse Handle(HttpHeaders headers, string raw);
    Task<SubscribeResult> Subscribe(SubscribeModel model, string? token = null);
    Task<Option<RegisteredSubscriptions>> Subscriptions(
        string? status = null,
        string? type = null,
        string? pagination = null,
        string? token = null
        );
    Task<bool> Unsubscribe(string id, string? token = null);
}