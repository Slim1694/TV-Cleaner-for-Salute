package ru.home.devmenu;

import android.app.Activity;
import android.content.Intent;
import android.os.Bundle;
import android.widget.Toast;

/** Opens the developer options screen (hidden on some Android TV firmwares) and closes itself. */
public class MainActivity extends Activity {
    private static final String TV_SETTINGS = "com.android.tv.settings";

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        Intent[] candidates = {
            new Intent("android.settings.APPLICATION_DEVELOPMENT_SETTINGS"),
            new Intent().setClassName(TV_SETTINGS, TV_SETTINGS + ".system.development.DevelopmentActivity"),
            new Intent("android.settings.SETTINGS")
        };
        boolean opened = false;
        for (Intent intent : candidates) {
            try {
                startActivity(intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK));
                opened = true;
                break;
            } catch (Exception ignored) {
                // try the next way to open it
            }
        }
        if (!opened) Toast.makeText(this, "Не удалось открыть меню разработчика", Toast.LENGTH_LONG).show();
        finish(); // Theme.NoDisplay requires finishing before onResume
    }
}
