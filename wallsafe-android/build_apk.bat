@echo off
set ANDROID_HOME=C:\Users\ASUS\AppData\Local\Android\Sdk
set JAVA_HOME=C:\Program Files\Android\Android Studio\jbr
cd /d "%~dp0"
call gradlew.bat :app:assembleDebug
