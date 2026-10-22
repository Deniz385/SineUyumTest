import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../data/models/diary.dart';
import '../../l10n/l10n.dart';

/// Bottom sheet for logging a watch (with an optional rating) or editing a
/// diary entry (date, rewatch flag and note only).
///
/// [onSubmit] does the request; if it throws, the sheet stays open and shows
/// the error (e.g. the server's "already logged that day" message) so the
/// date can be changed.
class DiaryEntrySheet extends StatefulWidget {
  const DiaryEntrySheet({
    super.key,
    required this.movieTitle,
    required this.onSubmit,
    this.entry,
    this.initialRating,
  });

  final String movieTitle;
  final Future<void> Function(DiaryDraft draft) onSubmit;

  /// The entry being edited; null when adding (then a rating can be given).
  final DiaryEntry? entry;

  /// Prefills the rating when adding (the user's current rating).
  final int? initialRating;

  static const noteMaxLength = 500;

  /// True once [onSubmit] succeeded; null if dismissed.
  static Future<bool?> show(
    BuildContext context, {
    required String movieTitle,
    required Future<void> Function(DiaryDraft draft) onSubmit,
    DiaryEntry? entry,
    int? initialRating,
  }) {
    return showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      builder: (_) => DiaryEntrySheet(
        movieTitle: movieTitle,
        onSubmit: onSubmit,
        entry: entry,
        initialRating: initialRating,
      ),
    );
  }

  @override
  State<DiaryEntrySheet> createState() => _DiaryEntrySheetState();
}

class _DiaryEntrySheetState extends State<DiaryEntrySheet> {
  late final _note = TextEditingController(text: widget.entry?.note ?? '');
  late DateTime _watchedOn = widget.entry?.watchedOn ?? _today;
  late bool _rewatch = widget.entry?.rewatch ?? false;
  late int? _rating = widget.initialRating;
  bool _saving = false;
  String? _error;

  bool get _isEdit => widget.entry != null;

  static DateTime get _today {
    final now = DateTime.now();
    return DateTime(now.year, now.month, now.day);
  }

  @override
  void dispose() {
    _note.dispose();
    super.dispose();
  }

  Future<void> _pickDate() async {
    final today = _today;
    final picked = await showDatePicker(
      context: context,
      initialDate: _watchedOn.isAfter(today) ? today : _watchedOn,
      firstDate: DateTime(1900),
      lastDate: today,
    );
    if (picked != null && mounted) {
      setState(() {
        _watchedOn = DateTime(picked.year, picked.month, picked.day);
        _error = null;
      });
    }
  }

  Future<void> _submit() async {
    if (_saving) return;
    setState(() {
      _saving = true;
      _error = null;
    });
    final note = _note.text.trim();
    try {
      await widget.onSubmit(
        DiaryDraft(
          watchedOn: _watchedOn,
          rewatch: _rewatch,
          note: note.isEmpty ? null : note,
          rating: _isEdit ? null : _rating,
        ),
      );
      if (mounted) Navigator.of(context).pop(true);
    } catch (error) {
      if (mounted) {
        setState(() {
          _saving = false;
          _error = error.toString();
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    final theme = Theme.of(context);
    final rating = _rating;

    return Padding(
      padding: EdgeInsets.only(bottom: MediaQuery.viewInsetsOf(context).bottom),
      child: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.fromLTRB(20, 20, 20, 12),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text(widget.movieTitle, style: theme.textTheme.titleMedium),
              const SizedBox(height: 4),
              Text(
                _isEdit ? l10n.diaryEditTitle : l10n.diarySheetPrompt,
                style: theme.textTheme.bodySmall,
              ),
              const SizedBox(height: 8),
              ListTile(
                contentPadding: EdgeInsets.zero,
                leading: const Icon(Icons.event_outlined),
                title: Text(l10n.diaryWatchedOn),
                subtitle: Text(
                  DateFormat.yMMMMEEEEd(l10n.localeName).format(_watchedOn),
                ),
                trailing: const Icon(Icons.edit_calendar_outlined),
                onTap: _saving ? null : _pickDate,
              ),
              SwitchListTile(
                contentPadding: EdgeInsets.zero,
                secondary: const Icon(Icons.replay),
                title: Text(l10n.diaryRewatch),
                subtitle: Text(l10n.diaryRewatchHint),
                value: _rewatch,
                onChanged: _saving
                    ? null
                    : (value) => setState(() => _rewatch = value),
              ),
              const SizedBox(height: 8),
              TextField(
                controller: _note,
                enabled: !_saving,
                maxLength: DiaryEntrySheet.noteMaxLength,
                minLines: 2,
                maxLines: 5,
                textCapitalization: TextCapitalization.sentences,
                decoration: InputDecoration(
                  labelText: l10n.diaryNoteLabel,
                  hintText: l10n.diaryNoteHint,
                  border: const OutlineInputBorder(),
                ),
              ),
              if (!_isEdit) ...[
                SwitchListTile(
                  contentPadding: EdgeInsets.zero,
                  secondary: const Icon(Icons.star_outline),
                  title: Text(l10n.diaryRateToo),
                  subtitle: Text(l10n.diaryRateTooHint),
                  value: rating != null,
                  onChanged: _saving
                      ? null
                      : (value) => setState(
                          () => _rating = value
                              ? (widget.initialRating ?? 7)
                              : null,
                        ),
                ),
                if (rating != null)
                  Row(
                    children: [
                      Expanded(
                        child: Slider(
                          value: rating.toDouble(),
                          min: 1,
                          max: 10,
                          divisions: 9,
                          label: '$rating',
                          onChanged: _saving
                              ? null
                              : (value) =>
                                    setState(() => _rating = value.round()),
                        ),
                      ),
                      SizedBox(
                        width: 56,
                        child: Text(
                          l10n.ratingOutOfTen(rating),
                          textAlign: TextAlign.center,
                          style: theme.textTheme.titleMedium,
                        ),
                      ),
                    ],
                  ),
              ],
              if (_error != null) ...[
                const SizedBox(height: 8),
                Text(
                  _error!,
                  style: theme.textTheme.bodyMedium?.copyWith(
                    color: theme.colorScheme.error,
                  ),
                ),
              ],
              const SizedBox(height: 12),
              FilledButton(
                onPressed: _saving ? null : _submit,
                child: _saving
                    ? const SizedBox.square(
                        dimension: 20,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : Text(_isEdit ? l10n.commonSave : l10n.diarySave),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
