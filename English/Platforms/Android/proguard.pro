# Proguard / R8 rules for .NET MAUI Release build

# Keep AndroidX and AppCompat components to prevent ClassNotFoundException during layout inflation
-keep class androidx.** { *; }
-keep interface androidx.** { *; }
-dontwarn androidx.**

# Specific classes required by AppCompatActivity
-keep class androidx.appcompat.widget.FitWindowsFrameLayout { *; }
-keep class androidx.core.app.CoreComponentFactory { *; }

# Keep Material Design components
-keep class com.google.android.material.** { *; }
-dontwarn com.google.android.material.**

# Keep .NET MAUI Java wrappers
-keep class crc64** { *; }
-dontwarn crc64**
