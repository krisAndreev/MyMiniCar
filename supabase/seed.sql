-- seed.sql — initial catalog, mirrors MockProductService
insert into public.products
  (id, name, description, price, category, image_url, default_material, dimensions, tile_class, is_featured, weight_grams, sort_order)
values
  ('golf-keychain', 'VW Golf IV Keychain',
   'The hot-hatch icon, shrunk to a pocket-sized keyring tag. Printed to order in your choice of PLA filament — add your name, plate or race number in the Studio.',
   16.90, 'Hatchback', 'images/cars/golf.png', 'Racing Red', '55mm × 30mm × 4mm', 'fil-red', true, 250, 1),
  ('audi-a4-keychain', 'Audi A4 (B6) Keychain',
   'The understated 2000s saloon, faithfully miniaturised. A clean, premium silhouette that prints beautifully in any finish.',
   16.90, 'Saloon', 'images/cars/audi-a4-2000.png', 'Silver Steel', '55mm × 30mm × 4mm', 'fil-silver', true, 250, 2),
  ('passat-keychain', 'VW Passat B5 Keychain',
   'The do-it-all family Volkswagen as a chunky keyring tag. Looks sharp in deep solid colours.',
   16.90, 'Saloon', 'images/cars/vw-passat.png', 'Racing Blue', '55mm × 30mm × 4mm', 'fil-blue', false, 250, 3),
  ('mercedes-w124-keychain', 'Mercedes W124 300CE Keychain',
   'The bulletproof modern classic. A coupé profile with the kind of presence that earns a premium finish.',
   18.90, 'Classic', 'images/cars/mercedes-w124-300ce.png', 'Marble PLA', '55mm × 30mm × 4mm', 'fil-marble', true, 250, 4),
  ('skoda-octavia-keychain', 'Skoda Octavia Keychain',
   'The dependable daily, miniaturised. A crisp three-box shape that prints clean in every filament.',
   16.90, 'Saloon', 'images/cars/skoda-octavia-2005.png', 'Wood Fill', '55mm × 30mm × 4mm', 'fil-wood', false, 250, 5),
  ('led-keyring', 'LED Light-Up Keyring',
   'Add-on hardware: a press-button LED keyring that clips to any MyMiniCar tag and lights up your custom car on demand.',
   12.00, 'Accessories', null, 'Midnight Black', 'Keyring · 28mm', 'fil-black', false, 250, 6),
  ('display-stand', 'Magnetic Display Stand',
   'A little printed plinth so your keychain doubles as a desk or shelf piece when it''s off the keys.',
   9.50, 'Accessories', null, 'Silver Steel', '40mm × 40mm × 18mm', 'fil-silver', false, 250, 7),
  ('hardware-pack', 'Keyring Hardware Pack',
   'Spare split-rings, lobster clips and ball-chains so you can rig your keychains exactly how you want.',
   5.00, 'Accessories', null, 'Midnight Black', 'Mixed hardware ×10', 'fil-black', false, 250, 8)
on conflict (id) do update set
  name = excluded.name, description = excluded.description, price = excluded.price,
  category = excluded.category, image_url = excluded.image_url,
  default_material = excluded.default_material, dimensions = excluded.dimensions,
  tile_class = excluded.tile_class, is_featured = excluded.is_featured,
  weight_grams = excluded.weight_grams, sort_order = excluded.sort_order,
  updated_at = now();

-- Bulgarian product text (English lives in name/description above).
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
