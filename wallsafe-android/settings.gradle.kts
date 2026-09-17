pluginManagement {
    repositories {
        google {
            content {
                includeGroupByRegex("com\\.android.*")
                includeGroupByRegex("com\\.google.*")
                includeGroupByRegex("androidx.*")
            }
        }
        mavenCentral()
        gradlePluginPortal()
    }
}

dependencyResolutionManagement {
    repositoriesMode.set(RepositoriesMode.FAIL_ON_PROJECT_REPOS)
    repositories {
        google()
        mavenCentral()
    }
}

rootProject.name = "WallSafe"

// App module
include(":app")

// Core modules
include(":core:model")
include(":core:common")
include(":core:network")
include(":core:database")
include(":core:datastore")
include(":core:data")
include(":core:ui")
include(":core:wallpaper")
include(":core:panic")

// Feature modules
include(":feature:home")
include(":feature:explore")
include(":feature:favorites")
include(":feature:downloads")
include(":feature:preview")
include(":feature:settings")

// Widget module
include(":widget")
