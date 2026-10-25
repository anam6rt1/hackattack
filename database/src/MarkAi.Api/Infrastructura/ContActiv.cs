using System.Collections.Concurrent;
using MarkAi.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;

namespace MarkAi.Api.Infrastructura;

/// <summary>Single-instance pilot: erasure cannot race an already authenticated profile/upload mutation.</summary>
public sealed class MutatiiCont : IDisposable
{
    private readonly ConcurrentDictionary<int, SemaphoreSlim> blocari = new();
    public SemaphoreSlim Pentru(int uid) => blocari.GetOrAdd(uid, _ => new SemaphoreSlim(1, 1));
    public void Dispose() { foreach (var b in blocari.Values) b.Dispose(); }
}

public sealed class ContActiv(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext h, MutatiiCont mutatii, MarkAiDbContext db)
    {
        if (h.User.Identity?.IsAuthenticated != true || HttpMethods.IsGet(h.Request.Method) || HttpMethods.IsHead(h.Request.Method) || HttpMethods.IsOptions(h.Request.Method))
        { await next(h); return; }
        var uid = h.User.Id();
        var blocare = mutatii.Pentru(uid);
        await blocare.WaitAsync(h.RequestAborted);
        try
        {
            if (!await db.Utilizatori.AsNoTracking().AnyAsync(u => u.Id == uid && u.StergereCerutaLa == null, h.RequestAborted))
            { h.Response.StatusCode = StatusCodes.Status401Unauthorized; return; }
            await next(h);
        }
        finally { blocare.Release(); }
    }
}

/// <summary>Existing real-time connections must also respect account erasure.</summary>
public sealed class ContActivJoc(MarkAiDbContext db, MutatiiCont mutatii) : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext context,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var uid = context.Context.User!.Id();
        var blocare = mutatii.Pentru(uid);
        await blocare.WaitAsync(context.Context.ConnectionAborted);
        try
        {
            if (!await db.Utilizatori.AsNoTracking().AnyAsync(u => u.Id == uid && u.StergereCerutaLa == null,
                context.Context.ConnectionAborted))
            {
                context.Context.Abort();
                throw new HubException("Contul nu mai este disponibil.");
            }
            return await next(context);
        }
        finally { blocare.Release(); }
    }
}
