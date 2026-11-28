# تحویل LMS_HUMS — مدیریت آموزشی و کارگاه محتوا

## مبنا و حدود تأیید

کد عملیاتی از `LMS_HUMS-workspace-source.zip` برداشته شد؛ دو ZIP قدیمی‌تر و قالب Frest مبنا نشدند. فایل‌های `lms-implementation-prompt.fa.md`، `README.fa.md` بستهٔ طراحی و `learning-workspace.html` خوانده شدند. هش HTML با سند تطبیق دارد:

```text
b6d2a1912f6ae6063db826c949f837cdcaa5bd507f5a0564e4e75746a9cb4f27
```

HTML توسط Chromium، تم روشن و DPR=1 رندر شد. هشت تصویر داخل [`learning-verification/reference`](learning-verification/reference/) بازتولید مرجع‌اند، نه اسکرین‌شات Blazor. فایل جانبی `visual/` جداگانه در دسترس نبود؛ CSS، تصویر Heart/ECG، آیکن‌های خطی، پنج thumbnail و فونت دارای OFL از همین HTML استخراج شدند. دادهٔ نمونه، PDF و ویدئوی نمونه، حساب‌ها، HTML عملیاتی ماکاپ، routeهای hash ماکاپ و ذخیره‌سازی IndexedDB وارد برنامه نشده‌اند.

**این بسته سورس قابل تحویل برای بیلد و تست است، نه انتشار عملیاتی تأییدشده.** SDK و SQL Server در محیط اجرا در دسترس نبودند؛ `dotnet --info`، `dotnet build` و `dotnet test` با کد ۱۲۷ متوقف شدند (مشاهدهٔ [`build-attempt.log`](learning-verification/build-attempt.log)). `has-pending-model-changes`، اجرای موتور Razor و migration و عکس مقایسه‌ای اجرای واقعی Blazor هنوز باید روی محیط دارای SDK/SQL انجام شوند. هیچ دیتابیسی در این تحویل تغییر نکرد.

## مسیرهای مدیر

| بخش | مسیر | رفتار |
|---|---|---|
| ساختار و تقویم آموزشی | `/lms/admin/catalog` | جدول دانشگاه با جست‌وجو و افزودن در بالا؛ نیم‌سال، اخذ و حذف‌واضافه در سه ستون مستقل با تاریخ جلالی. |
| درس‌ها | `/lms/admin/courses` | انتخاب رشته، جست‌وجو بر نام/کد/رشته، ویرایش و ورود XLSX. یک کد می‌تواند در دو رشته وجود داشته باشد. |
| استادان | `/lms/admin/teachers` | جدول، جست‌وجو و فیلتر قلمرو، ثبت و ویرایش با استعلام، و ورود اکسل موجود. |
| دانشجویان | `/lms/admin/students` | جدول مستقل، شماره و ورودی، سوابق و ورود اکسل موجود. `/lms/admin/people` همچنان به فهرست استادان سازگار با پیوندهای قبلی می‌رسد. |
| ارائه دروس | `/lms/offerings/admin` | هر ارائه همچنان گروهِ یک درس در یک نیم‌سال است. جست‌وجو و صفحه‌بندی سمت سرور و ورود اکسل در بالای جدول. |
| نقش‌ها و مجوزها | `/lms/admin/roles` | در لایوت ادمین LMS، با همان Policy و API نقش‌های قبلی؛ فهرست نقش‌ها و فهرست مجوزها جدول هستند. کنسول قدیمی حذف نشده است. |
| تصویر استاد | منوی حساب، بالای سمت چپ لایوت مدرس | تصویر قبلی و امکان تغییر در AccountMenu با همان pipeline خصوصی؛ تغییر تم و خروج حفظ شده‌اند. |

`AppDataTable` برای جدول‌های LMS شمار پیش‌فرض ۱۵ و انتخاب ۱۵، ۳۰، ۵۰ یا ۱۰۰ را ارائه می‌کند. جدول‌های مدیریتی ارائه‌ها و اشخاص، پس از محدودکردن نتایج به قلمرو در SQL صفحه‌بندی می‌شوند؛ لاگ‌ها هم صفحه‌بندی سمت سرور دارند. جدول‌های کوچکی که قبلاً فهرست کامل مجاز را می‌آوردند، در مرورگر صفحه‌بندی می‌شوند. سقف‌های قبلی خارج از دامنه، از جمله تعداد نمونه‌های داشبورد، دست‌نخورده‌اند.

### ورود فایل XLSX

- قالب‌ها: `src/ReusableAccess.Web/wwwroot/lms-templates/courses.xlsx` و `offerings.xlsx`. در قسمت مربوط دکمهٔ «دریافت قالب XLSX» وجود دارد. ردیف اول هدر و ردیف‌های بعدی داده‌اند؛ حداکثر ۵۰۰ ردیف، فقط Sheet1 و سقف ۸٬۰۰۰٬۰۰۰ بایت. کدها متن‌اند تا صفر آغازین از دست نرود.
- درس‌ها: `course_code, title`؛ رشته در UI انتخاب می‌شود. ارائه‌ها: `course_code, group_code, instructor_code`؛ رشته و نیم‌سال در UI انتخاب می‌شوند. کد استاد همان **کد سازمانی استاد فعال و یکتا در همان رشته** است، نه کد ملی. ورود گروهی ارائهٔ تخصصی و منتشرنشده می‌سازد؛ مخاطب عمومی/مشترک و ورودی هدف در فرم عادی با مجوزهای قبلی مدیریت می‌شوند.
- ابتدا پیش‌نمایش بدون ثبت، سپس ثبت ردیف‌های معتبر با اعتبارسنجی مجدد. ردیف موجود بازنویسی نمی‌شود؛ حتی در صورت ارسال مجدد فایل. نتیجه ردیف‌به‌ردیف برگردانده می‌شود؛ ثبت‌های موفق در بستهٔ دارای ردیف نامعتبر عمداً باقی می‌مانند. فرمول، ادغام دسترسی‌ها و انتخاب قلمرو خارج از مجوز پذیرفته نمی‌شوند.

## ممیزی و نگاشت آموزشی

مسیرها نسبت به ریشهٔ سورس گزارش شده‌اند. «پیشنهادی در مبنا» یعنی کمبود در ورودی که در این بسته پیاده شده یا محدودیتش در ادامه مشخص است.

| نیاز | شاهد و منبع حقیقت | وضعیت در مبنا | تغییر این نسخه و اثر روی دادهٔ قدیمی | شاهد/آزمون |
|---|---|---|---|---|
| ارائهٔ درس و معرفی | `Domain/Academics/AcademicEntities.cs`، `Offering` و `CatalogCourse` در `src/ReusableAccess.Domain` | عنوان از درس پایه؛ معرفی مخصوص ارائه غایب | عنوان مشترک باقی است؛ `DraftIntroduction` و `PublishedIntroduction` به ارائه اضافه شدند، بدون تولید متن ساختگی یا تغییر عنوان سایر ارائه‌ها. | آزمون `LearningReleaseKeepsStableProgressAndDoesNotExposeDraftOrFutureMaterials` و UI `LearningHeader.razor`. |
| درس به ازای رشته | `src/ReusableAccess.Infrastructure/Academics/LmsCatalog.cs:SaveAsync` / `AcademicModel.cs` | تعلق و یکتایی بر دانشکده | رشته برای ثبت درس جدید لازم است. `FacultyId` برای مجوز/ارائهٔ تاریخی مشتق و حفظ می‌شود. FK مرکب هم‌دانشکده بودن را تضمین می‌کند. | `LegacyFacultyCourseIsNotAssignedToAnArbitraryMajor`؛ V4. |
| فصل | `Chapter` در `AcademicEntities.cs`، انتشار تک‌فصل در `LmsOfferings.PublishChapterAsync` | فصل و ترتیب/زمان موجود؛ خلاصه و نسخهٔ ویرایش‌پذیر پس از انتشار غایب | `Summary` و نسخهٔ کاری هم‌فصل. عدم تغییر فصل قدیمی به‌جز نشانهٔ خروج از نمای جاری پس از جایگزینی. | آزمون یکپارچهٔ انتشار، `LearningCommands.OpenDraft`. |
| درس | `Lesson` و `LessonRow`؛ `LmsOfferings.ChapterRows` | خلاصه، متن و کاور درسک و ترتیب موجود | طول ستون خلاصه افزایشی از ۲۵۰ به ۳۲۰؛ `StableLessonId` با FK به درس اصلی، بدون تغییر URL/هویت پیشرفت؛ تاریخ‌های واقعی حفظ می‌شوند. | سناریوی دو درسک با زمان‌های متفاوت و تکمیل قدیمی. |
| منبع | `Lesson.Body`، `LessonFile`، `Media` | متن/فایل بدون عنوانِ جدا/توضیح/نوعِ مدل‌شده | `LearningMaterial` با نوع، عنوان، توضیح، بدنه، فایل، alt و کاور. خواندن قدیمی به‌وسیلهٔ adapter؛ نه کپی مخرب و نه seed. پیوست‌های قدیمی DOCX/XLSX قابل دانلودند. | آزمون انواع و `LearningQueries.Material`. |
| ترتیب | `Chapter.Position`، `Lesson.Position`، `LessonFile.Position` | ترتیب و محدودیت یکتا در مدل موجود | ترتیب تاریخی حفظ؛ خواندن پایدار `Position` و کلید tie-breaker؛ رکورد تازه انتهای همان والد. ابزار جدید جابه‌جایی در کارگاه اضافه نشده است. | ناوبری قبلی/بعدی در `LearningQueries.Lesson`. |
| کاور | کاور تک‌درس در `Lesson.CoverMediaId` | جزئی | `FeaturedMaterialKey` در درس و `CoverId` در منبع؛ Keep/Remove/Replace صریح؛ بررسی تعلق هنگام نوشتن. پیش‌فرض‌ها از دارایی‌های HTML، پوستر و کارت از `LearningUi.Cover` و fallback یک‌بار در `learning.js`. | سناریوی کاور معتبر، رد دسترسی از کاربر ثبت‌نام‌نشده، قبل/بعد انتشار. |
| انتشار و زمان‌بندی | `Offering` زمان انتشار/نمایش؛ `AcademicRules`؛ `StudioScheduling` | فصل منتشرشده ثابت؛ مسیر مستقل Studio | فصل منتشرشده مبنای ثابت می‌ماند. تغییرِ فصل منتشرشده نسخهٔ کاری می‌سازد؛ در تراکنش با revision، نسخهٔ قبلی از نمای جاری کنار می‌رود. انتشار همهٔ تغییرها یا یک فصل، بدون نسخهٔ تازه برای فرم بدون تفاوت مؤثر. Studio، تاریخچهٔ برنامه، انتشار رسمی خود ارائه و فصل‌های خارج از انتخاب حفظ‌اند. | `LearningValidation.Changes/Check`، آزمون retry و تعارض؛ گیت‌های زمان `LearningAccess` و `AcademicRules`. |
| فایل و پخش | `LmsFiles`، `LmsBff`، Route موجود `/bff/lms/files/{id}` | مخزن خصوصی، antiforgery، Range و اعتبارسنجی اولیه | نوع و اندازهٔ تطبیق‌یافته؛ فایل اصلی و کاور جدا و منتشرشده/پیش‌نویس با گیت واقعی درس. رکورد می‌تواند در قالب یا فصل قبلی هم فایل را نگه دارد. PNG ساختاری و zlib محدود؛ JPEG/GIF/WebP امضا/کانتینر و ابعاد، **نه decode پیکسلی کامل در سرور**؛ AV و آزمون پخش واقعی باقی است. | تست‌هایی برای PNG و دسترسی. تعیین سقف ۲۵ MiB و ۳ MiB در `UploadLimits`، XSS encode معمول Razor. |
| پیشرفت | `LessonProgress` در `LmsEnrollments` | دارای تکمیل یک‌طرفه؛ بدون لغو یا مخرج نمای جاری | همان جدول و هویت ثبت‌نام؛ `ClearedUtc` برای لغو بدون حذف ردیف و حفظ زمان تاریخی. به‌روزرسانی صریح و idempotent؛ پیش‌نمایش نوشتن ندارد. شمارنده براساس درس‌های مجاز فعلی. | تست دو دانشجو (مجاز و ثبت‌نام‌نشده)، لغو تکمیل و شمارندهٔ بعد از تغییر نسخه. |
| الگوها و تب‌ها | `StudioDocuments`، `StudioMaterializer` و `LmsOffering` | موجود | `StudioResource` و خلاصهٔ فصل در نسخهٔ خصوصی دادهٔ تازه را نگه می‌دارند. تب آنلاین همچنان نمایشی؛ تکلیف و گفتگو route و مجوز خود را دارند. کتابخانهٔ الگو مستقل از تکلیف و محتوای منتشرشده است. | چارچوب قبلی و تست‌های `StudioPersistenceTests` با فراخوانی اصلاح‌شدهٔ `AssessmentEdit`. |

`LearningRequests.cs`، `AcademicAdminRequests.cs` و `LmsEndpoints` مسیر خواندن را جدا از فرمان تغییر نگه می‌دارند. مدل پرس‌وجوی دانشجو فقط پس از احراز نقش، ثبت‌نام و انتشار/زمان‌بندی همان ارائه پاسخ می‌دهد. مسیرهای `/lms/offer/{id}/teacher?tab=content&step=…`، `/preview` (نسخهٔ منتشرشده)، `/preview?tab=content&draft=true` (پیش‌نویس مجاز)، `/student` و زیرمسیر `/learn/{lesson}` یا `/learn/{lesson}/{material}` واقعی‌اند. شناسهٔ برداشت‌شده از URL به والد و ارائه محدود می‌شود.

## ارتقا و بیلد

مهاجرت جدید: `20261126183000_LearningCurriculumAndMajorCourses` بعد از `20261126093000_SeparateOfferingWorkspace`. فایل‌های مهاجرت پیشین و helperهای snapshot آن‌ها تغییر نکرده‌اند. backfill فقط وقتی `MajorId` می‌گذارد که **همهٔ** ارائه‌های موجود درس تخصصی باشند و به یک رشتهٔ همان دانشکده اشاره کنند. موارد بدون شاهد، مشترک یا مبهم `null` می‌مانند و در UI با «نیازمند تعیین رشته» قابل‌بررسی‌اند. هیچ تخصیص رشته‌ای به حدس از روی دانشکده انجام نمی‌شود. فایل، زمان‌ها، تحویل‌های تکلیف، سابقه، درسک و فصل قدیمی حذف یا بازنویسی نمی‌شوند. `Down` برای حذف اشیای V4 عمداً تعریف نشده؛ بازگشت نیازمند backup آزمایش‌شده و بررسی ثبت‌های بعد از ارتقاست.

در ریشهٔ پروژه و با .NET 10 و اتصال به نسخهٔ **آزمایشی** پایگاه، دستورهای زیر را اجرا کنید. زمان اتصال از تنظیمات API و `ConnectionStrings:DefaultConnection` می‌آید. از کلیدهای محیطی واقعی فقط در میزبان استفاده کنید؛ هیچ اسراری در ZIP لازم نیست.

```bash
dotnet tool restore
dotnet restore ReusableAccess.slnx
dotnet build ReusableAccess.slnx --configuration Release
dotnet test ReusableAccess.slnx --configuration Release --no-build
dotnet ef migrations list --project src/ReusableAccess.Infrastructure --startup-project src/ReusableAccess.Api --context ApplicationDbContext --configuration Release --no-build
dotnet ef migrations has-pending-model-changes --project src/ReusableAccess.Infrastructure --startup-project src/ReusableAccess.Api --context ApplicationDbContext --configuration Release --no-build
dotnet ef migrations script --idempotent --project src/ReusableAccess.Infrastructure --startup-project src/ReusableAccess.Api --context ApplicationDbContext --configuration Release --no-build --output lms-learning-upgrade.sql
```

برای تست SQL پارامتر `RA_SQLSERVER_TEST_CONNECTION` را روی سرور ایزوله‌ای با اجازهٔ ایجاد DB تستی بگذارید. تست‌ها پایگاه‌های تصادفی `RA_LmsTest_…` می‌سازند و پاک می‌کنند. اسکریپت EF را مستقل بررسی و تنها پس از backup و تست بازیابی روی کپی اجرا کنید؛ اگر به‌جای اسکریپت در توسعه از `dotnet ef database update` استفاده می‌کنید، **همان** پروژه، startup project، context، config و دیتابیس API را هدف بگیرید. از `-c Release` استفاده نکنید؛ `-c` نام کوتاه context است. بدون مشاهدهٔ ستون‌ها/تاریخچه و SQL تاییدشده، این بسته را جایگزین سرور عملیاتی نکنید.

### وضعیت تطبیق بصری

در fixture CSS جداگانه که از DOM خود مرجع تغذیه شد، ابعاد Hero، عرض thumbnail و ستون‌های محتوا در ۱۴۴۰، ۱۲۷۰، ۱۰۸۰، ۹۰۱، ۹۰۰، ۷۳۰، ۶۴۰، ۵۲۵، ۳۹۰ و ۳۶۰ پیکسل با مرجع برابر بودند و سرریز افقی در fixture دیده نشد. اعداد در [`geometry.json`](learning-verification/geometry.json) آمده‌اند. این آزمایش جای Blazor و MudBlazor عملیاتی را نمی‌گیرد؛ عکس بعد از اجرای واقعی موجود نیست. کنترل‌های حساب، نقش، تغییر تم، زمان نمایش، ثبت‌نام، قالب، تب‌ها و انتقال فایل که نباید حذف شوند تفاوت‌های عمدی با مرجع هستند. کنتراست تم تیره در scope محتوا تعریف شده، ولی مطابق سند مرجع مقایسهٔ پیکسلی فقط برای تم روشن امکان‌پذیر است.
