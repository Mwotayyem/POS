import { useState, type FormEvent } from 'react';
import { profileApi } from '../api/endpoints';
import { useAuth } from '../auth/AuthContext';
import { errorMessage } from '../lib/format';
import { ErrorBanner, Field, PageHeader } from '../components/ui';

export function ProfilePage() {
  const { profile } = useAuth();
  const [fullName, setFullName] = useState(profile?.fullName ?? '');
  const [phone, setPhone] = useState(profile?.phone ?? '');
  const [profileMsg, setProfileMsg] = useState<{ ok: boolean; text: string } | null>(null);

  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [pwdMsg, setPwdMsg] = useState<{ ok: boolean; text: string } | null>(null);

  async function saveProfile(e: FormEvent) {
    e.preventDefault();
    setProfileMsg(null);
    try {
      await profileApi.update({ fullName: fullName.trim(), phone: phone.trim() || null });
      setProfileMsg({ ok: true, text: 'تم حفظ الملف الشخصي.' });
    } catch (err) {
      setProfileMsg({ ok: false, text: errorMessage(err) });
    }
  }

  async function changePassword(e: FormEvent) {
    e.preventDefault();
    setPwdMsg(null);
    try {
      await profileApi.changePassword({ currentPassword, newPassword });
      setPwdMsg({ ok: true, text: 'تم تغيير كلمة المرور.' });
      setCurrentPassword('');
      setNewPassword('');
    } catch (err) {
      setPwdMsg({ ok: false, text: errorMessage(err) });
    }
  }

  return (
    <div className="stack" style={{ maxWidth: 560 }}>
      <PageHeader title="الملف الشخصي" />

      <form onSubmit={saveProfile} className="card stack">
        <h2 style={{ fontSize: 17, margin: 0 }}>البيانات</h2>
        {profileMsg &&
          (profileMsg.ok ? (
            <div className="alert alert-success">{profileMsg.text}</div>
          ) : (
            <ErrorBanner message={profileMsg.text} />
          ))}
        <Field label="البريد الإلكتروني">
          <input className="input" value={profile?.email ?? ''} disabled dir="ltr" />
        </Field>
        <Field label="الاسم الكامل">
          <input className="input" value={fullName} onChange={(e) => setFullName(e.target.value)} required />
        </Field>
        <Field label="الهاتف">
          <input className="input" value={phone} onChange={(e) => setPhone(e.target.value)} dir="ltr" />
        </Field>
        <div className="row" style={{ justifyContent: 'flex-end' }}>
          <button className="btn" type="submit">حفظ</button>
        </div>
        <div className="muted" style={{ fontSize: 13 }}>
          الأدوار: {profile?.roles.join('، ') || '-'}
        </div>
      </form>

      <form onSubmit={changePassword} className="card stack">
        <h2 style={{ fontSize: 17, margin: 0 }}>تغيير كلمة المرور</h2>
        {pwdMsg &&
          (pwdMsg.ok ? (
            <div className="alert alert-success">{pwdMsg.text}</div>
          ) : (
            <ErrorBanner message={pwdMsg.text} />
          ))}
        <Field label="كلمة المرور الحالية">
          <input
            className="input"
            type="password"
            value={currentPassword}
            onChange={(e) => setCurrentPassword(e.target.value)}
            required
            dir="ltr"
          />
        </Field>
        <Field label="كلمة المرور الجديدة">
          <input
            className="input"
            type="password"
            value={newPassword}
            onChange={(e) => setNewPassword(e.target.value)}
            required
            dir="ltr"
          />
        </Field>
        <div className="row" style={{ justifyContent: 'flex-end' }}>
          <button className="btn" type="submit">تغيير</button>
        </div>
      </form>
    </div>
  );
}
