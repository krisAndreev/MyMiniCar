using System;
using System.Collections.Generic;
using MyMiniCar.Web.Models;

namespace MyMiniCar.Web.Services;

public enum SiteLanguage
{
    English,
    Bulgarian
}

public class LanguageService
{
    private readonly Dictionary<string, string> _bg = new()
    {
        ["MyMiniCar | 3D-Printed Keychains of Your Car"] = "MyMiniCar | 3D принтирани мини модели на твоята кола",
        ["Toggle navigation"] = "Отвори навигацията",
        ["Switch language"] = "Смени езика",
        ["Cart"] = "Количка",
        ["Log in"] = "Вход",
        ["Log out"] = "Изход",
        ["My orders"] = "Моите поръчки",
        ["Home"] = "Начало",
        ["Shop"] = "Магазин",
        ["Studio"] = "Студио",
        ["How it works"] = "Как работи",
        ["Make yours"] = "Създай своя",
        ["Design yours now"] = "Създай своя сега",
        ["Browse keychains"] = "Разгледай моделите",
        ["Browse the shop"] = "Разгледай магазина",
        ["Open the Studio"] = "Отвори студиото",
        ["Open Studio"] = "Отвори студиото",
        ["Design another"] = "Създай друг модел",
        ["Continue shopping"] = "Продължи пазаруването",
        ["Back to checkout"] = "Назад към плащането",

        ["3D-printed · made to order"] = "3D принтирано · по поръчка",
        ["Your car, shrunk to fit your"] = "Твоята кола, смалена за",
        ["pocket"] = "джоба ти",
        ["Pick your model, choose your colour and size, and watch it spin in 3D before you buy. We print it to order and ship it to your door."] = "Избери модел, цвят и размер, и го виж как се върти в 3D преди поръчка. Принтираме го по поръчка и го изпращаме до теб.",
        ["from"] = "от",
        ["30-day remake guarantee · free shipping over $40 · no account needed"] = "30 дни гаранция за преработка · безплатна доставка над $40 · без нужда от профил",
        ["4.9/5 from 2,000+ drivers"] = "4.9/5 от 2 000+ шофьори",
        ["Printing today — ships in 3–5 days"] = "Принтира се днес — изпраща се за 3-5 дни",
        ["Pick your model"] = "Избери модел",
        ["Choose your car from the catalogue — Golf, Passat, W124 and more."] = "Избери кола от каталога — Golf, Passat, W124 и още.",
        ["Customise & preview"] = "Персонализирай и виж",
        ["Choose your colour and size. See exactly how it'll look, live in 3D."] = "Избери цвят и размер. Виж точно как ще изглежда на живо в 3D.",
        ["We print & ship"] = "Принтираме и изпращаме",
        ["Printed to order in 3–5 days and sent to your door, keyring attached."] = "Изработва се по поръчка за 3-5 дни и пристига готово с халка.",
        ["Fan favourites"] = "Любими модели",
        ["Popular keychains"] = "Популярни модели",
        ["View all"] = "Виж всички",
        ["Add to cart"] = "Добави в количката",
        ["Customise"] = "Персонализирай",
        ["Loved by drivers"] = "Любими на шофьорите",
        ["2,000+ minis on keyrings worldwide"] = "2 000+ мини модела по ключове из целия свят",
        ["Verified"] = "Потвърдено",
        ["30-day guarantee"] = "30 дни гаранция",
        ["Don't love it? We reprint or refund."] = "Не ти харесва? Принтираме отново или връщаме парите.",
        ["Free shipping over $40"] = "Безплатна доставка над $40",
        ["Tracked, worldwide."] = "С проследяване, по целия свят.",
        ["Secure checkout"] = "Сигурно плащане",
        ["Cards, PayPal & Apple Pay."] = "Карти, PayPal и Apple Pay.",
        ["Printed to order"] = "По поръчка",
        ["Made for you in 3–5 days."] = "Изработено за теб за 3-5 дни.",
        ["The Studio"] = "Студиото",
        ["Your car, your colours — in under a minute."] = "Твоята кола, твоите цветове — за под минута.",
        ["Jump into the Studio and design a one-of-a-kind mini of your car. Nothing to install — it all happens right in your browser."] = "Влез в студиото и създай уникален мини модел на своята кола. Нищо не се инсталира — всичко става в браузъра.",
        ["Order today — printing starts tomorrow"] = "Поръчай днес — принтирането започва утре",

        ["The Shop"] = "Магазинът",
        ["Keychains & accessories"] = "Ключодържатели и аксесоари",
        ["Ready-made styles and add-ons — or head to the Studio to design your own."] = "Готови модели и добавки — или влез в студиото, за да създадеш свой.",
        ["All"] = "Всички",
        ["Sort"] = "Сортиране",
        ["Featured"] = "Препоръчани",
        ["Price: low to high"] = "Цена: възходящо",
        ["Price: high to low"] = "Цена: низходящо",
        ["Nothing matches that filter."] = "Няма резултати за този филтър.",
        ["Details"] = "Детайли",
        ["Filament finish"] = "Финиш на филамента",
        ["Size"] = "Размер",
        ["Made to order"] = "По поръчка",
        ["3–5 days"] = "3-5 дни",
        ["Hatchback"] = "Хечбек",
        ["Saloon"] = "Седан",
        ["Classic"] = "Класика",
        ["Accessories"] = "Аксесоари",
        ["Custom"] = "Персонални",

        ["Your cart"] = "Твоята количка",
        ["Your cart is empty."] = "Количката ти е празна.",
        ["Make a keychain"] = "Създай ключодържател",
        ["Subtotal"] = "Междинна сума",
        ["Checkout"] = "Плащане",
        ["Free shipping over $40 · printed in 3–5 days"] = "Безплатна доставка над $40 · изработка за 3-5 дни",
        ["Shipping"] = "Доставка",
        ["Total"] = "Общо",
        ["Free"] = "Безплатно",
        ["All keychains"] = "Всички модели",
        ["Bundles"] = "Комплекти",
        ["Premium"] = "Премиум",
        ["Make"] = "Изработка",
        ["Materials"] = "Материали",
        ["Get 10% off your first tag"] = "Вземи 10% отстъпка за първия си модел",
        ["New filament drops and offers, no spam."] = "Нови цветове и оферти, без спам.",
        ["Printed to order with care."] = "Изработено по поръчка с внимание.",
        ["Privacy"] = "Поверителност",
        ["Terms"] = "Условия",
        ["We turn a photo of your car into a 3D-printed keyring tag — printed to order in real PLA filament and shipped worldwide."] = "Превръщаме снимка на колата ти в 3D принтиран модел — изработен по поръчка от истински PLA филамент и изпратен до теб.",

        ["Almost there"] = "Почти готово",
        ["Enter your details, choose Econt delivery, then pay securely with card. We print to order in 3–5 days."] = "Въведи данните си, избери доставка с Еконт и плати сигурно с карта. Принтираме по поръчка за 3-5 дни.",
        ["Payment was canceled — your cart is still here whenever you're ready."] = "Плащането беше отказано — количката ти е запазена, когато си готов.",
        ["Your details"] = "Твоите данни",
        ["Email"] = "Имейл",
        ["Full name"] = "Име и фамилия",
        ["Phone"] = "Телефон",
        ["Delivery"] = "Доставка",
        ["To address"] = "До адрес",
        ["To Econt office"] = "До офис на Еконт",
        ["City"] = "Град",
        ["Loading cities..."] = "Зареждаме градове...",
        ["— select a city —"] = "— избери град —",
        ["Address"] = "Адрес",
        ["Street, number, floor — anything a courier can find"] = "Улица, номер, етаж — всичко нужно за куриера",
        ["Office"] = "Офис",
        ["Loading offices..."] = "Зареждаме офиси...",
        ["— select an office —"] = "— избери офис —",
        ["No Econt offices in this city. Try delivery to address instead."] = "Няма офиси на Еконт в този град. Опитай доставка до адрес.",
        ["Choose a city first to list its offices."] = "Първо избери град, за да видиш офисите.",
        ["Calculating Econt delivery..."] = "Изчисляваме доставка с Еконт...",
        ["Redirecting to secure checkout..."] = "Пренасочване към сигурно плащане...",
        ["Pay"] = "Плати",
        ["Payments processed securely by Stripe. Test mode — use card 4242 4242 4242 4242."] = "Плащанията се обработват сигурно от Stripe. Тестов режим — използвай карта 4242 4242 4242 4242.",
        ["Order summary"] = "Обобщение на поръчката",
        ["Add"] = "Добави",
        ["more for free shipping, or calculate Econt delivery above."] = "още за безплатна доставка или изчисли доставка с Еконт по-горе.",
        ["Couldn't reach Econt just now — applying a standard €5.90 rate."] = "В момента няма връзка с Еконт — прилагаме стандартна цена €5.90.",
        ["Your order qualifies for free shipping — Econt cost is covered."] = "Поръчката ти получава безплатна доставка — цената на Еконт е покрита.",
        ["Econt delivery:"] = "Доставка с Еконт:",
        ["We couldn't start the payment. Please make sure the payments service is running and try again."] = "Не успяхме да стартираме плащането. Увери се, че платежната услуга работи, и опитай отново.",
        ["We couldn't reach the payments service. Please try again in a moment."] = "Не успяхме да се свържем с платежната услуга. Опитай отново след малко.",

        ["Confirming your payment..."] = "Потвърждаваме плащането...",
        ["Thank you!"] = "Благодарим!",
        ["Your order is confirmed"] = "Поръчката ти е потвърдена",
        ["a receipt is on its way to"] = "разписка пътува към",
        ["We'll start printing your keychain right away."] = "Започваме принтирането веднага.",
        ["Arranging Econt delivery..."] = "Организираме доставката с Еконт...",
        ["Econt tracking:"] = "Проследяване с Еконт:",
        ["Payment not completed"] = "Плащането не е завършено",
        ["We couldn't confirm a successful payment for this order."] = "Не успяхме да потвърдим успешно плащане за тази поръчка.",
        ["No order found"] = "Не е намерена поръчка",
        ["This page is shown after a successful checkout."] = "Тази страница се показва след успешно плащане.",
        ["Couldn't verify order"] = "Не успяхме да потвърдим поръчката",
        ["We couldn't reach the payments service to confirm your order. If you were charged, your receipt email is the source of truth."] = "Не успяхме да се свържем с платежната услуга, за да потвърдим поръчката. Ако си таксуван, имейлът с разписка е водещ.",
        ["Payment pending"] = "Плащането се обработва",
        ["Your payment hasn't completed yet. If you just paid, give it a moment and refresh."] = "Плащането още не е завършено. Ако току-що плати, изчакай малко и презареди.",

        ["Get your own"] = "Създай свой",
        ["keychain"] = "ключодържател",
        ["figure"] = "мини модел",
        ["Pick your car, make it yours — see it spin in 3D, live."] = "Избери кола, направи я своя — и я виж как се върти в 3D на живо.",
        ["Your"] = "Твоят",
        ["live"] = "на живо",
        ["Updating live"] = "Обновява се",
        ["Your model"] = "Твоят модел",
        ["3D preview ready"] = "Готов 3D преглед",
        ["Your colour"] = "Твоят цвят",
        ["Your size"] = "Твоят размер",
        ["It's yours — added!"] = "Твой е — добавено!",
        ["Drag to rotate"] = "Влачи за завъртане",
        ["Drag to rotate · scroll to zoom"] = "Влачи за завъртане · скролирай за приближаване",

        ["From photo to keyring"] = "От снимка до ключодържател",
        ["No 3D modelling, no software, no waiting around. Here's how your car ends up on your keys."] = "Без 3D моделиране, без софтуер и без излишно чакане. Ето как колата ти стига до ключовете.",
        ["1 · Upload"] = "1 · Качване",
        ["Pick any photo of your car in the Studio. We shape it into a car-tag silhouette and show you a live preview instantly."] = "Избери снимка на колата в студиото. Оформяме я като силует и веднага показваме преглед.",
        ["2 · Customise"] = "2 · Персонализация",
        ["Choose from eight PLA filaments and add your name, plate or race number with a ready-made text template."] = "Избери от осем PLA филамента и добави име, номер или състезателен номер с готов шаблон.",
        ["3 · Printed & shipped"] = "3 · Принтиране и доставка",
        ["We print your tag to order on an FDM printer, attach the keyring, and ship it within 3–5 days."] = "Принтираме модела по поръчка на FDM принтер, добавяме халка и го изпращаме до 3-5 дни.",
        ["Real PLA filament"] = "Истински PLA филамент",
        ["Every keychain is printed in genuine PLA — a sturdy, lightweight bioplastic. Standard colours are included; glow, marble and wood-fill are our premium finishes for something special."] = "Всеки ключодържател се принтира от истински PLA — здрав и лек биопластмасов материал. Стандартните цветове са включени; светещият, мраморният и дървесният финиш са премиум варианти.",
        ["Tag length"] = "Дължина",
        ["Thickness"] = "Дебелина",
        ["Recyclable PLA"] = "Рециклируем PLA",
        ["Ready to make yours?"] = "Готов ли си да създадеш своя?",

        ["Midnight Black"] = "Нощно черно",
        ["Pure White"] = "Чисто бяло",
        ["Racing Red"] = "Състезателно червено",
        ["Racing Blue"] = "Състезателно синьо",
        ["Silver Steel"] = "Сребриста стомана",
        ["Glow in the Dark"] = "Светещ в тъмното",
        ["Marble PLA"] = "Мраморен PLA",
        ["Wood Fill"] = "Дървесен филамент",
        ["Matte PLA"] = "Матов PLA",
        ["Glossy Resin"] = "Гланцова смола",
        ["Brushed Aluminium"] = "Матиран алуминий",
        ["Wood Composite"] = "Дървесен композит",
        ["Marble Composite"] = "Мраморен композит",
        ["Die-cast Metal"] = "Лят метал",
        ["Carbon Fiber"] = "Карбонови влакна",
        ["Standard"] = "Стандартен",
        ["Showcase"] = "Витринен",
        ["Keychain"] = "Ключодържател",
        ["Desk Figure"] = "Фигура за бюро",
        ["Display Figure"] = "Витринна фигура",
        ["Pocket-sized, with a keyring + text plate"] = "Джобен размер с халка и текстова плочка",
        ["A standalone mini for your desk"] = "Самостоятелен мини модел за бюро",
        ["Bigger, with the finest detail"] = "По-голям, с най-фин детайл"
    };

    public SiteLanguage Current { get; private set; } = SiteLanguage.English;
    public bool IsBulgarian => Current == SiteLanguage.Bulgarian;
    public string CurrentCode => IsBulgarian ? "BG" : "EN";

    public event Action? OnChange;

    public string T(string text) =>
        IsBulgarian && _bg.TryGetValue(text, out var translated) ? translated : text;

    public void Toggle()
    {
        Current = IsBulgarian ? SiteLanguage.English : SiteLanguage.Bulgarian;
        OnChange?.Invoke();
    }

    public string ProductName(Product product)
    {
        if (!IsBulgarian) return product.Name;
        if (!string.IsNullOrWhiteSpace(product.NameBg)) return product.NameBg;

        // Studio-generated products aren't in the DB — translate their name inline.
        if (product.Id.StartsWith("custom-", StringComparison.OrdinalIgnoreCase)
            && product.Name.StartsWith("Custom ", StringComparison.OrdinalIgnoreCase))
        {
            var customName = product.Name["Custom ".Length..];
            return $"Персонализиран {customName}";
        }

        return T(product.Name);
    }

    public string ProductDescription(Product product) =>
        IsBulgarian && !string.IsNullOrWhiteSpace(product.DescriptionBg)
            ? product.DescriptionBg
            : product.Description;
}
