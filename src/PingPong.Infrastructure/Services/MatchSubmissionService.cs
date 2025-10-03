using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PingPong.Application.Interfaces;
using PingPong.Application.Models;
using PingPong.Domain.Aggregates;
using PingPong.Domain.Entities;
using PingPong.Domain.Exceptions;
using PingPong.Domain.ValueObjects;
using PingPong.Infrastructure.Persistence;

namespace PingPong.Infrastructure.Services;

public sealed class MatchSubmissionService : IMatchSubmissionService
{
    private readonly PingPongDbContext _context;
    private readonly IPlayerDirectory _playerDirectory;

    public MatchSubmissionService(PingPongDbContext context, IPlayerDirectory playerDirectory)
    {
        _context = context;
        _playerDirectory = playerDirectory;
    }

    public async Task<MatchSubmissionResult> SubmitMatchAsync(MatchSubmissionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Sets is null || request.Sets.Count == 0)
        {
            throw new DomainValidationException("At least one set score must be provided.");
        }

        var playerOne = await _playerDirectory.EnsurePlayerAsync(request.PlayerOneName, cancellationToken);
        var playerTwo = await _playerDirectory.EnsurePlayerAsync(request.PlayerTwoName, cancellationToken);

        var setScores = request.Sets
            .OrderBy(set => set.SetNumber)
            .Select(set => new MatchSetScore(set.PlayerOneScore, set.PlayerTwoScore))
            .ToList();

        var submittedAt = DateTimeOffset.UtcNow;
        var aggregate = MatchAggregate.CreateNew(
            playerOne.Id,
            playerTwo.Id,
            request.MatchDate,
            setScores,
            submittedAt,
            request.SubmittedBy);

        aggregate.Match.PlayerOne = playerOne;
        aggregate.Match.PlayerTwo = playerTwo;
        aggregate.Event.PlayerOne = playerOne;
        aggregate.Event.PlayerTwo = playerTwo;

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        _context.Matches.Add(aggregate.Match);
        await _context.SaveChangesAsync(cancellationToken);

        aggregate.Event.MatchId = aggregate.Match.Id;
        await _context.MatchEvents.AddAsync(aggregate.Event, cancellationToken);
        aggregate.Match.Events.Add(aggregate.Event);

        aggregate.Match.PrimaryEventId = aggregate.Event.Id;
        aggregate.Match.LatestEventId = aggregate.Event.Id;
        _context.Matches.Update(aggregate.Match);

        await _context.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new MatchSubmissionResult(aggregate.Match.Id, aggregate.Event.Id);
    }
}
