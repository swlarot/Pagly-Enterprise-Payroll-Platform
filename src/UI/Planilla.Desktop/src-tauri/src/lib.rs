// Planilla Desktop — Entry point de la app Tauri v2
// La lógica principal está en el frontend React (ClientApp).
// Este archivo es el mínimo requerido por Tauri.

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    tauri::Builder::default()
        .plugin(tauri_plugin_opener::init())
        .run(tauri::generate_context!())
        .expect("Error al ejecutar la app Planilla Desktop");
}
