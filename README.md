# Nusa HR - HR dan Payroll Internal

Aplikasi ASP.NET Core Razor Pages dengan PostgreSQL untuk data karyawan dan
proses payroll perusahaan sekitar 500 karyawan.

## Fitur saat ini

- Login dengan ASP.NET Core Identity, penguncian sementara setelah kegagalan
  login berulang, dan peran Admin, HR, Finance, Manager, serta Employee.
- Admin dapat membuat akun dan memberikan satu peran.
- Data karyawan, status aktif/nonaktif, serta impor CSV UTF-8 dengan validasi
  nomor karyawan duplikat; data dapat diperbarui dan diekspor kembali ke CSV.
- Payroll per periode 21 bulan sebelumnya sampai tanggal 20 bulan berjalan.
  Pembayaran awal tanggal 30 dan dapat diubah Finance. Finance dapat memasukkan
  tunjangan, penyesuaian prorata/lembur, potongan lain, PPh 21, dan iuran BPJS
  karyawan sebagai nominal yang telah diverifikasi; payroll dapat diekspor CSV.
- Alur status Draft → Tinjau → Disetujui → Dibayar; nilai payroll dikunci saat
  proses tinjau dimulai.
- Pencatatan absensi, jam lembur, dan permohonan cuti beserta persetujuan
  Manager/HR. Karyawan dapat masuk ke portal untuk mengajukan cuti dan melihat
  slip gaji yang telah disetujui.

> **Belum siap untuk produksi tanpa konfigurasi dan uji perusahaan:** jam
> lembur dicatat tetapi tarifnya belum ditentukan; Finance memasukkan nilai
> lembur dan prorata manual (jadwal shift produksi belum dimodelkan). Absensi
> dan cuti tidak otomatis mengubah gaji. PPh 21/BPJS belum dihitung otomatis;
> Finance harus menghitung serta memverifikasi nominal. Status "Dibayar" hanya
> penanda manual, bukan transfer bank. Versi ini belum memiliki audit log
> perubahan, pengaturan tarif/pajak, kalender shift/hari libur, PDF slip gaji,
> atau backup terjadwal. Uji semua kebijakan, siapkan backup dan pemulihan
> PostgreSQL, serta tinjau keamanan sebelum memasukkan data gaji nyata.
> Inisialisasi skema otomatis saat ini hanya untuk database kosong; perubahan
> skema berikutnya harus memakai migrasi database yang ditinjau sebelum database
> berisi data dipakai ulang.

## Persiapan Windows

1. Pasang .NET 10 SDK dan PostgreSQL.
2. Buat database kosong `payroll_demo` melalui pgAdmin atau perintah SQL:

   ```sql
   CREATE DATABASE payroll_demo;
   ```

3. Buka PowerShell pada folder project. Atur kredensial di sesi terminal saat
   ini (ganti nilai contoh dengan kredensial PostgreSQL lokal dan kata sandi
   admin yang kuat; jangan simpan rahasia di source control):

   ```powershell
   $env:ConnectionStrings__PayrollDatabase = "Host=localhost;Port=5432;Database=payroll_demo;Username=postgres;Password=GANTI_PASSWORD_POSTGRES"
   $env:BootstrapAdmin__Email = "admin@perusahaan.local"
   $env:BootstrapAdmin__Password = "GANTI_DENGAN_KATA_SANDI_KUAT_12_KARAKTER"
   dotnet run --urls http://localhost:5187
   ```

   Kata sandi minimal 12 karakter dan harus memuat huruf besar/kecil, angka,
   serta simbol. Akun administrator hanya dibuat jika email tersebut belum ada.
   Database/tabel dibuat otomatis saat aplikasi pertama kali berjalan.
4. Buka `http://localhost:5187`, lalu masuk menggunakan email dan kata sandi
   administrator di atas. Buat akun HR/Finance melalui menu **Pengguna & akses**.
5. Hentikan aplikasi dengan `Ctrl+C`.

`.\run.bat` hanya menjalankan aplikasi; variabel lingkungan dan database tetap
harus disiapkan lebih dahulu.

## Akses jaringan kantor

Jalankan awalnya di komputer/server internal, bukan langsung membuka port aplikasi
ke jaringan. Sebelum dipakai karyawan melalui LAN, pasang HTTPS (misalnya IIS
dengan sertifikat internal/reverse proxy), batasi firewall hanya untuk jaringan
kantor, amankan kredensial PostgreSQL, dan siapkan backup serta prosedur
pemulihan. Jangan gunakan HTTP biasa untuk mengirim kata sandi atau data gaji.

Impor CSV memerlukan header berikut (gaji tanpa pemisah ribuan, tanggal ISO):

```csv
employeeNumber,fullName,department,position,email,monthlySalary,joinDate
EMP-001,Nama Karyawan,Finance,Staff,nama@perusahaan.local,12500000,2024-01-15
```
