-- 0004_product_i18n.sql — Bulgarian product text (English stays in name/description).
alter table public.products add column if not exists name_bg        text;
alter table public.products add column if not exists description_bg text;

update public.products p set
  name_bg = v.name_bg,
  description_bg = v.description_bg
from (values
  ('golf-keychain',
   'Ключодържател VW Golf IV',
   'Иконата сред хечбеците, смалена до джобен ключодържател. Принтира се по поръчка в избран PLA филамент — добави име, номер или състезателен номер в студиото.'),
  ('audi-a4-keychain',
   'Ключодържател Audi A4 (B6)',
   'Сдържаният седан от 2000-те, миниатюризиран с внимание. Чист премиум силует, който изглежда отлично във всеки финиш.'),
  ('passat-keychain',
   'Ключодържател VW Passat B5',
   'Универсалният семеен Volkswagen като плътен ключодържател. Стои страхотно в наситени плътни цветове.'),
  ('mercedes-w124-keychain',
   'Ключодържател Mercedes W124 300CE',
   'Неразрушимата модерна класика. Купе профил с присъствие, което заслужава премиум финиш.'),
  ('skoda-octavia-keychain',
   'Ключодържател Skoda Octavia',
   'Надеждният ежедневен автомобил в мини размер. Чиста форма, която се принтира добре във всеки филамент.'),
  ('led-keyring',
   'LED светеща халка',
   'Допълнителен аксесоар: LED халка с бутон, която се закача към всеки MyMiniCar модел и го осветява при нужда.'),
  ('display-stand',
   'Магнитна стойка',
   'Малка принтирана стойка, с която ключодържателят става детайл за бюро или рафт, когато не е на ключовете.'),
  ('hardware-pack',
   'Комплект халки и аксесоари',
   'Резервни халки, клипсове и верижки, за да закачиш моделите точно както искаш.')
) as v(id, name_bg, description_bg)
where p.id = v.id;
