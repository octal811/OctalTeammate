using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OctalPulse.Domain.Entities;

namespace OctalPulse.Application.Services;

/// <summary>
/// Handles offline, encrypted or atomic JSON persistence for personal notes,
/// including a secondary Emergency Folder backup independent from the application install root.
/// </summary>
public interface INotesStorageService
{
    string PrimaryStoragePath { get; }
    string EmergencyStoragePath { get; }

    Task<List<UserNote>> LoadNotesAsync(CancellationToken cancellationToken = default);
    Task SaveNotesAsync(IEnumerable<UserNote> notes, CancellationToken cancellationToken = default);
    Task<UserNote> AddOrUpdateNoteAsync(UserNote note, CancellationToken cancellationToken = default);
    Task DeleteNoteAsync(Guid noteId, CancellationToken cancellationToken = default);
    Task<bool> HasEmergencyBackupAsync(CancellationToken cancellationToken = default);
    Task<List<UserNote>> RestoreFromEmergencyBackupAsync(CancellationToken cancellationToken = default);
}
