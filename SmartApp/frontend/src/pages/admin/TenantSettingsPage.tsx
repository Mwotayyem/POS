import { useEffect, useState, type FormEvent } from 'react';
import { settingsApi } from '../../api/endpoints';
import type { TenantSettings } from '../../api/models';
import { useAuth } from '../../auth/AuthContext';
import { P } from '../../auth/permissions';
import { useAsync } from '../../lib/useAsync';
import { errorMessage } from '../../lib/format';
import { ErrorBanner, Field, Loading, PageHeader } from '../../components/ui';

export function TenantSettingsPage() {
  const { hasPermission } = useAuth();
  const canManage = hasPermission(P.settings.manage);
  const { data, loading, error } = useAsync(() => settingsApi.get(), []);
  const [form, setForm] = useState<TenantSettings | null>(null);
  const [msg, setMsg] = useState<{ ok: boolean; text: string } | null>(null);

  useEffect(() => {
    if (data) setForm(data);
  }, [data]);

  async function save(e: FormEvent) {
    e.preventDefault();
    if (!form) return;
    setMsg(null);
    try {
      await settingsApi.update({
        ...form,
        currency: form.currency.trim(),
        timeZone: form.timeZone.trim(),
        locale: form.locale.trim(),
        themeJson: form.themeJson?.trim() || null,
      });
      setMsg({ ok: true, text: 'تم حفظ الإعدادات.' });
    } catch (err) {
      setMsg({ ok: false, text: errorMessage(err) });
    }
  }

  return (
    <div className="stack" style={{ maxWidth: 560 }}>
      <PageHeader title="إعدادات المستأجر" />
      {loading && <Loading />}
      {error && <ErrorBanner message={error} />}
      {form && (
        <form onSubmit={save} className="card stack">
          {msg &&
            (msg.ok ? <div className="alert alert-success">{msg.text}</div> : <ErrorBanner message={msg.text} />)}
          <Field label="العملة (ISO، 3 أحرف)">
            <input className="input" value={form.currency} maxLength={3} onChange={(e) => setForm({ ...form, currency: e.target.value })} disabled={!canManage} dir="ltr" />
          </Field>
          <Field label="المنطقة الزمنية">
            <input className="input" value={form.timeZone} onChange={(e) => setForm({ ...form, timeZone: e.target.value })} disabled={!canManage} dir="ltr" />
          </Field>
          <Field label="نسبة الضريبة الافتراضية (%)">
            <input className="input" type="number" min={0} max={100} value={form.defaultTaxRate} onChange={(e) => setForm({ ...form, defaultTaxRate: Number(e.target.value) })} disabled={!canManage} />
          </Field>
          <Field label="اللغة (Locale)">
            <input className="input" value={form.locale} onChange={(e) => setForm({ ...form, locale: e.target.value })} disabled={!canManage} dir="ltr" />
          </Field>
          <Field label="الثيم (JSON اختياري)">
            <textarea className="input" rows={3} value={form.themeJson ?? ''} onChange={(e) => setForm({ ...form, themeJson: e.target.value })} disabled={!canManage} dir="ltr" />
          </Field>
          {canManage && (
            <div className="row" style={{ justifyContent: 'flex-end' }}>
              <button className="btn" type="submit">حفظ</button>
            </div>
          )}
        </form>
      )}
    </div>
  );
}
