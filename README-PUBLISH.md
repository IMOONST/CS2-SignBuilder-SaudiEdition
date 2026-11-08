# نشر "Sign Builder: Saudi Edition" على Paradox Mods

> English summary at the end.

- المشروع: `SignBuilder/SignBuilderSA.csproj` (التجميعة `SignBuilderSA`، مجلد التثبيت `Mods/SignBuilderSA`، مجلد البيانات `ModsData/SignBuilderSA`).
- ملف النشر: `SignBuilder/Properties/PublishConfiguration.xml` (الاسم، الوصف العربي والإنجليزي، سجل التغييرات، الصور).
- الصورة المصغّرة الجديدة: `SignBuilder/Properties/Thumbnail.png` (1024×1024). صورة المود الأصلي محفوظة باسم `Thumbnail-original.png` ولا تُنشر.

## 1. لقطات الشاشة المطلوبة

الصور القديمة `Screenshot1.png` … `Screenshot6.png` في مجلد Properties هي صور المود الأصلي — **لا تحذفها ولا تكتب فوقها**. احفظ صورك الجديدة بالأسماء التالية (هي المذكورة في ملف النشر):

| الملف | ماذا تُظهر |
|---|---|
| `SignBuilder/Properties/SA-Screenshot1.png` | جسر (Gantry) فوق طريق سريع عليه لوحتان إرشاديتان خضراوان بالعربي والإنجليزي (مثل: الرياض Riyadh مع درع رقم الطريق وسهم). لقطة نهارية من زاوية سائق. |
| `SignBuilder/Properties/SA-Screenshot2.png` | نافذة المصمّم بالواجهة العربية، ونمط "السعودية" مختار، ومكتبة القوالب مفتوحة على فئة ظاهرة (إرشادية أو خدمات). |
| `SignBuilder/Properties/SA-Screenshot3.png` | لوحة بسطر يجمع العربي والإنجليزي والأرقام، مع قائمة الخطوط العربية مفتوحة (Cairo / Tajawal / Amiri) لإظهار الكتابة الصحيحة. |
| `SignBuilder/Properties/SA-Screenshot4.png` | لوحات تحذيرية وتنظيمية على جانب الطريق: تحذير الجمال، حد السرعة، قف، مع رموز المكتبة السعودية. |
| `SignBuilder/Properties/SA-Screenshot5.png` | لقطة ليلية أو في ضباب/مطر: لوحة رسائل إلكترونية LED متوهجة، ولوحة "سرعتك" بوجه مبتسم أو حزين. |
| `SignBuilder/Properties/SA-Screenshot6.png` | لوحة "مدير اللوحات" (القائمة والبحث)، أو اختيار طريق مع تعبئة اسم الشارع والحي والسرعة تلقائياً. |

نصائح:
- الدقة المقترحة **1920×1080** (16:9)، بصيغة PNG، ويُفضّل أن يكون حجم كل صورة أقل من 2–3 ميجابايت تقريباً.
- استخدم وضع التصوير (Photo Mode) وأخفِ واجهة اللعبة في لقطات المدينة؛ وأبقِ الواجهة ظاهرة في لقطات المصمّم والمدير.
- لا تُظهر مودات أخرى تشتت الانتباه أو أي معلومات شخصية.
- إن أردت عدداً مختلفاً من الصور فعدّل أسطر `<Screenshot>` في ملف النشر لتطابق الملفات الموجودة فعلاً (الأداة تفشل إن كان ملف مذكور غير موجود).

## 2. قبل النشر (قائمة التحقق)

- [ ] **لا يوجد عنصر `ModId`** في ملف النشر. (المعرّف 165726 كان للمود الأصلي وتمت إزالته — إرجاعه يعني محاولة الكتابة فوق مود Andy Smith.)
- [ ] `ModVersion` = `1.0.0` و `GameVersion` = `1.6.*` (عدّل الثانية إن تغيّر إصدار اللعبة).
- [ ] `DisplayName` = `Sign Builder: Saudi Edition`.
- [ ] `AccessLevel`: القيمة الحالية `Public`. القيم المقبولة: `Public` أو `Unlisted` أو `Private` أو `Hidden`. للتجربة أولاً يمكنك النشر بـ `Private` ثم التحويل إلى `Public` بأمر التحديث.
- [ ] الصور الست الجديدة موجودة بالأسماء أعلاه.
- [ ] التراخيص والشكر: ملف `LICENSE` (MIT مع اسم Andy Smith)، ملفات OFL للخطوط في `Resources/Fonts`، و `Resources/Symbols/sa/CREDITS.txt` — تأكد أنها تُنسخ مع المود في مجلد `Mods/SignBuilderSA`.
- [ ] جرّب المود في **لعبة نظيفة** (بدون Sign Builder الأصلي وبدون مودات أخرى): إنشاء لوحة سعودية، تركيبها على جسر وعلى جانب الطريق، الحفظ ثم إعادة التحميل، تبديل اللغة، ومدير اللوحات.
- [ ] راجع ملفات السجل في مجلد `Logs` داخل بيانات اللعبة (مثل `Player.log` و `UI.log`) للتأكد من عدم وجود أخطاء.
- [ ] تأكد من البناء بإعداد Release بدون أخطاء: `dotnet build SignBuilder/SignBuilderSA.csproj -c Release`.

## 3. تسجيل الدخول بحساب Paradox

أداة النشر (ModPublisher) تستخدم حساب Paradox المسجّل في اللعبة تلقائياً. لذلك:
1. شغّل اللعبة وسجّل الدخول بحساب Paradox (من القائمة الرئيسية) مرة واحدة على الأقل، ثم أغلقها.
2. يجب أن تكون أدوات التطوير (Modding Toolchain) مثبّتة من: الإعدادات ← Modding.

إن ظهرت الرسالة "Could not auto log in: You were not logged in before" فسجّل الدخول في اللعبة أولاً ثم أعد المحاولة.

## 4. أوامر النشر

نفّذها من مجلد المشروع:

```powershell
cd D:\CitiesSkylinesMegaMod\CS2-SignBuilder-main\SignBuilder

# أول نشر (ينشئ مود جديد ويكتب ModId في ملف النشر تلقائياً)
dotnet publish SignBuilderSA.csproj -c Release /p:PublishProfile=PublishNewMod

# إصدار جديد لاحقاً (ارفع ModVersion وحدّث ChangeLog أولاً)
dotnet publish SignBuilderSA.csproj -c Release /p:PublishProfile=PublishNewVersion

# تحديث الوصف/الصور فقط بدون رفع ملفات جديدة
dotnet publish SignBuilderSA.csproj -c Release /p:PublishProfile=UpdatePublishedConfiguration
```

بعد أول نشر ناجح ستظهر في السجل رسالة `Mod published with Id=...` — **احتفظ بملف النشر بعد أن تضيف الأداة الـ ModId إليه** (أو أضفه يدوياً `<ModId Value="الرقم" />`) لأن الإصدارات اللاحقة تحتاجه.

## 5. بعد النشر
- افتح صفحة المود على mods.paradoxplaza.com وتأكد من الصور والوصف بالعربي.
- اشترك في المود من داخل اللعبة وجرّبه بعد حذف النسخة المحلية من `Mods/SignBuilderSA`.

---

## English summary

- Project: `SignBuilder/SignBuilderSA.csproj` (assembly `SignBuilderSA`, deploy folder `Mods/SignBuilderSA`, data `ModsData/SignBuilderSA`).
- Screenshots: save six new 1920×1080 PNGs as `Properties/SA-Screenshot1.png` … `SA-Screenshot6.png` (gantry with bilingual green guide signs; Arabic designer UI with the Saudi template library; mixed Arabic/English line with the Arabic font list; roadside warning/regulatory signs; LED board and "Your Speed" sign at night/fog; sign manager or road pick). Do not overwrite the original mod's `Screenshot1-6.png`.
- Checklist: no `ModId` on first publish (the original's 165726 was removed), ModVersion 1.0.0, GameVersion 1.6.*, AccessLevel (Public/Unlisted/Private/Hidden), MIT + OFL + symbol credits shipped, tested in a clean game without the original Sign Builder.
- Log in to your Paradox account in the game once (the publisher auto-logs in with it), then run from `SignBuilder/`:
  `dotnet publish SignBuilderSA.csproj -c Release /p:PublishProfile=PublishNewMod` (first), then `PublishNewVersion` for updates, or `UpdatePublishedConfiguration` for description/images only.
