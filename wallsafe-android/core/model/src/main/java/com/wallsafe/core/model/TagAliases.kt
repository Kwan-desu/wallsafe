package com.wallsafe.core.model

data class TagAlias(
    val tag: String,
    val title: String,
    val category: TagCategory = TagCategory.Series
)

object TagAliases {
    val aliases: Map<String, TagAlias> = mapOf(
        // Franchises & Series
        "wuwa" to TagAlias("wuthering_waves", "Wuthering Waves", TagCategory.Series),
        "wuthering" to TagAlias("wuthering_waves", "Wuthering Waves", TagCategory.Series),
        "wuthering wave" to TagAlias("wuthering_waves", "Wuthering Waves", TagCategory.Series),
        "wuthering waves" to TagAlias("wuthering_waves", "Wuthering Waves", TagCategory.Series),
        "genshin" to TagAlias("genshin_impact", "Genshin Impact", TagCategory.Series),
        "gi" to TagAlias("genshin_impact", "Genshin Impact", TagCategory.Series),
        "genshin impact" to TagAlias("genshin_impact", "Genshin Impact", TagCategory.Series),
        "hsr" to TagAlias("honkai:_star_rail", "Honkai: Star Rail", TagCategory.Series),
        "star rail" to TagAlias("honkai:_star_rail", "Honkai: Star Rail", TagCategory.Series),
        "starrail" to TagAlias("honkai:_star_rail", "Honkai: Star Rail", TagCategory.Series),
        "honkai star rail" to TagAlias("honkai:_star_rail", "Honkai: Star Rail", TagCategory.Series),
        "hi3" to TagAlias("honkai_impact_3rd", "Honkai Impact 3rd", TagCategory.Series),
        "honkai" to TagAlias("honkai_impact_3rd", "Honkai Impact 3rd", TagCategory.Series),
        "honkai impact" to TagAlias("honkai_impact_3rd", "Honkai Impact 3rd", TagCategory.Series),
        "zzz" to TagAlias("zenless_zone_zero", "Zenless Zone Zero", TagCategory.Series),
        "zenless" to TagAlias("zenless_zone_zero", "Zenless Zone Zero", TagCategory.Series),
        "zenless zone zero" to TagAlias("zenless_zone_zero", "Zenless Zone Zero", TagCategory.Series),
        "fgo" to TagAlias("fate/grand_order", "Fate/Grand Order", TagCategory.Series),
        "fate" to TagAlias("fate/grand_order", "Fate/Grand Order", TagCategory.Series),
        "fate grand order" to TagAlias("fate/grand_order", "Fate/Grand Order", TagCategory.Series),
        "ba" to TagAlias("blue_archive", "Blue Archive", TagCategory.Series),
        "blue archive" to TagAlias("blue_archive", "Blue Archive", TagCategory.Series),
        "al" to TagAlias("azur_lane", "Azur Lane", TagCategory.Series),
        "azur lane" to TagAlias("azur_lane", "Azur Lane", TagCategory.Series),
        "ak" to TagAlias("arknights", "Arknights", TagCategory.Series),
        "arknights" to TagAlias("arknights", "Arknights", TagCategory.Series),
        "miku" to TagAlias("hatsune_miku", "Hatsune Miku", TagCategory.Character),
        "hatsune miku" to TagAlias("hatsune_miku", "Hatsune Miku", TagCategory.Character),
        "csm" to TagAlias("chainsaw_man", "Chainsaw Man", TagCategory.Series),
        "chainsaw man" to TagAlias("chainsaw_man", "Chainsaw Man", TagCategory.Series),
        "aot" to TagAlias("shingeki_no_kyojin", "Attack on Titan", TagCategory.Series),
        "attack on titan" to TagAlias("shingeki_no_kyojin", "Attack on Titan", TagCategory.Series),
        "jjk" to TagAlias("jujutsu_kaisen", "Jujutsu Kaisen", TagCategory.Series),
        "jujutsu" to TagAlias("jujutsu_kaisen", "Jujutsu Kaisen", TagCategory.Series),
        "jujutsu kaisen" to TagAlias("jujutsu_kaisen", "Jujutsu Kaisen", TagCategory.Series),
        "rezero" to TagAlias("re:zero_kara_hajimeru_isekai_seikatsu", "Re:Zero", TagCategory.Series),
        "re zero" to TagAlias("re:zero_kara_hajimeru_isekai_seikatsu", "Re:Zero", TagCategory.Series),
        "sao" to TagAlias("sword_art_online", "Sword Art Online", TagCategory.Series),
        "sword art online" to TagAlias("sword_art_online", "Sword Art Online", TagCategory.Series),
        "bocchi" to TagAlias("bocchi_the_rock!", "Bocchi the Rock!", TagCategory.Series),
        "bocchi the rock" to TagAlias("bocchi_the_rock!", "Bocchi the Rock!", TagCategory.Series),
        "holo" to TagAlias("hololive", "Hololive", TagCategory.Series),
        "hololive" to TagAlias("hololive", "Hololive", TagCategory.Series),
        "dandadan" to TagAlias("dandadan", "Dandadan", TagCategory.Series),
        "frieren" to TagAlias("sousou_no_frieren", "Frieren", TagCategory.Series),
        "lycoris" to TagAlias("lycoris_recoil", "Lycoris Recoil", TagCategory.Series),
        "lycoris recoil" to TagAlias("lycoris_recoil", "Lycoris Recoil", TagCategory.Series),
        "nier" to TagAlias("nier_(series)", "NieR", TagCategory.Series),
        "eva" to TagAlias("neon_genesis_evangelion", "Evangelion", TagCategory.Series),
        "evangelion" to TagAlias("neon_genesis_evangelion", "Evangelion", TagCategory.Series),
        "madoka" to TagAlias("mahou_shoujo_madoka_magica", "Madoka Magica", TagCategory.Series),
        "touhou" to TagAlias("touhou", "Touhou", TagCategory.Series),
        "2hu" to TagAlias("touhou", "Touhou", TagCategory.Series),
        "nikke" to TagAlias("goddess_of_victory:_nikke", "Nikke", TagCategory.Series),
        "goddess of victory nikke" to TagAlias("goddess_of_victory:_nikke", "Nikke", TagCategory.Series),
        "kancolle" to TagAlias("kantai_collection", "Kantai Collection", TagCategory.Series),
        "kantai collection" to TagAlias("kantai_collection", "Kantai Collection", TagCategory.Series),

        // Characters with parenthesized franchise / Booru names
        "azki" to TagAlias("azki_(hololive)", "AZKi (Hololive)", TagCategory.Character),
        "suisei" to TagAlias("hoshimachi_suisei", "Hoshimachi Suisei (Hololive)", TagCategory.Character),
        "hoshimachi suisei" to TagAlias("hoshimachi_suisei", "Hoshimachi Suisei (Hololive)", TagCategory.Character),
        "fubuki" to TagAlias("shirakami_fubuki", "Shirakami Fubuki (Hololive)", TagCategory.Character),
        "shirakami fubuki" to TagAlias("shirakami_fubuki", "Shirakami Fubuki (Hololive)", TagCategory.Character),
        "pekora" to TagAlias("usada_pekora", "Usada Pekora (Hololive)", TagCategory.Character),
        "usada pekora" to TagAlias("usada_pekora", "Usada Pekora (Hololive)", TagCategory.Character),
        "marine" to TagAlias("houshou_marine", "Houshou Marine (Hololive)", TagCategory.Character),
        "houshou marine" to TagAlias("houshou_marine", "Houshou Marine (Hololive)", TagCategory.Character),
        "miko" to TagAlias("sakura_miko", "Sakura Miko (Hololive)", TagCategory.Character),
        "sakura miko" to TagAlias("sakura_miko", "Sakura Miko (Hololive)", TagCategory.Character),
        "gura" to TagAlias("gawr_gura", "Gawr Gura (Hololive)", TagCategory.Character),
        "gawr gura" to TagAlias("gawr_gura", "Gawr Gura (Hololive)", TagCategory.Character),
        "korone" to TagAlias("inugami_korone", "Inugami Korone (Hololive)", TagCategory.Character),
        "inugami korone" to TagAlias("inugami_korone", "Inugami Korone (Hololive)", TagCategory.Character),
        "okayu" to TagAlias("nekomata_okayu", "Nekomata Okayu (Hololive)", TagCategory.Character),
        "nekomata okayu" to TagAlias("nekomata_okayu", "Nekomata Okayu (Hololive)", TagCategory.Character),
        "aqua" to TagAlias("minato_aqua", "Minato Aqua (Hololive)", TagCategory.Character),
        "minato aqua" to TagAlias("minato_aqua", "Minato Aqua (Hololive)", TagCategory.Character),
        "ayame" to TagAlias("nakiri_ayame", "Nakiri Ayame (Hololive)", TagCategory.Character),
        "nakiri ayame" to TagAlias("nakiri_ayame", "Nakiri Ayame (Hololive)", TagCategory.Character),
        "towa" to TagAlias("tokoyami_towa", "Tokoyami Towa (Hololive)", TagCategory.Character),
        "tokoyami towa" to TagAlias("tokoyami_towa", "Tokoyami Towa (Hololive)", TagCategory.Character),
        "botan" to TagAlias("shishiro_botan", "Shishiro Botan (Hololive)", TagCategory.Character),
        "shishiro botan" to TagAlias("shishiro_botan", "Shishiro Botan (Hololive)", TagCategory.Character),
        "koyori" to TagAlias("hakui_koyori", "Hakui Koyori (Hololive)", TagCategory.Character),
        "hakui koyori" to TagAlias("hakui_koyori", "Hakui Koyori (Hololive)", TagCategory.Character),
        "chloe" to TagAlias("sakamata_chloe", "Sakamata Chloe (Hololive)", TagCategory.Character),
        "sakamata chloe" to TagAlias("sakamata_chloe", "Sakamata Chloe (Hololive)", TagCategory.Character),
        "laplus" to TagAlias("la+_darknesss", "La+ Darknesss (Hololive)", TagCategory.Character),
        "laplus darknesss" to TagAlias("la+_darknesss", "La+ Darknesss (Hololive)", TagCategory.Character),
        "calliope" to TagAlias("mori_calliope", "Mori Calliope (Hololive)", TagCategory.Character),
        "mori calliope" to TagAlias("mori_calliope", "Mori Calliope (Hololive)", TagCategory.Character),
        "kiara" to TagAlias("takanashi_kiara", "Takanashi Kiara (Hololive)", TagCategory.Character),
        "takanashi kiara" to TagAlias("takanashi_kiara", "Takanashi Kiara (Hololive)", TagCategory.Character),
        "ina" to TagAlias("ninomae_ina'nis", "Ninomae Ina'nis (Hololive)", TagCategory.Character),
        "ninomae ina'nis" to TagAlias("ninomae_ina'nis", "Ninomae Ina'nis (Hololive)", TagCategory.Character),
        "kronii" to TagAlias("ouro_kronii", "Ouro Kronii (Hololive)", TagCategory.Character),
        "ouro kronii" to TagAlias("ouro_kronii", "Ouro Kronii (Hololive)", TagCategory.Character),
        "fuwamoco" to TagAlias("fuwawa_abyssgard", "Fuwamoco (Hololive)", TagCategory.Character),
        "bijou" to TagAlias("koseki_bijou", "Koseki Bijou (Hololive)", TagCategory.Character),
        "koseki bijou" to TagAlias("koseki_bijou", "Koseki Bijou (Hololive)", TagCategory.Character),
        "shiori" to TagAlias("shiori_novella", "Shiori Novella (Hololive)", TagCategory.Character),
        "shiori novella" to TagAlias("shiori_novella", "Shiori Novella (Hololive)", TagCategory.Character),
        "nerissa" to TagAlias("nerissa_ravencroft", "Nerissa Ravencroft (Hololive)", TagCategory.Character),
        "nerissa ravencroft" to TagAlias("nerissa_ravencroft", "Nerissa Ravencroft (Hololive)", TagCategory.Character),
        "raden" to TagAlias("juufuutei_raden", "Juufuutei Raden (Hololive)", TagCategory.Character),
        "juufuutei raden" to TagAlias("juufuutei_raden", "Juufuutei Raden (Hololive)", TagCategory.Character),
        "ao" to TagAlias("hiodoshi_ao", "Hiodoshi Ao (Hololive)", TagCategory.Character),
        "hiodoshi ao" to TagAlias("hiodoshi_ao", "Hiodoshi Ao (Hololive)", TagCategory.Character),
        "ririka" to TagAlias("ichijou_ririka", "Ichijou Ririka (Hololive)", TagCategory.Character),
        "ichijou ririka" to TagAlias("ichijou_ririka", "Ichijou Ririka (Hololive)", TagCategory.Character),
        "kanade" to TagAlias("otonose_kanade", "Otonose Kanade (Hololive)", TagCategory.Character),
        "otonose kanade" to TagAlias("otonose_kanade", "Otonose Kanade (Hololive)", TagCategory.Character),
        "hajime" to TagAlias("todoroki_hajime", "Todoroki Hajime (Hololive)", TagCategory.Character),
        "todoroki hajime" to TagAlias("todoroki_hajime", "Todoroki Hajime (Hololive)", TagCategory.Character),

        // Wuthering Waves characters
        "changli" to TagAlias("changli_(wuthering_waves)", "Changli (Wuthering Waves)", TagCategory.Character),
        "jinshi" to TagAlias("jinhsi_(wuthering_waves)", "Jinhsi (Wuthering Waves)", TagCategory.Character),
        "jinhsi" to TagAlias("jinhsi_(wuthering_waves)", "Jinhsi (Wuthering Waves)", TagCategory.Character),
        "yinlin" to TagAlias("yinlin_(wuthering_waves)", "Yinlin (Wuthering Waves)", TagCategory.Character),
        "rover" to TagAlias("rover_(wuthering_waves)", "Rover (Wuthering Waves)", TagCategory.Character),
        "camellya" to TagAlias("camellya_(wuthering_waves)", "Camellya (Wuthering Waves)", TagCategory.Character),
        "shorekeeper" to TagAlias("the_shorekeeper_(wuthering_waves)", "The Shorekeeper (Wuthering Waves)", TagCategory.Character),
        "the shorekeeper" to TagAlias("the_shorekeeper_(wuthering_waves)", "The Shorekeeper (Wuthering Waves)", TagCategory.Character),
        "yangyang" to TagAlias("yangyang_(wuthering_waves)", "Yangyang (Wuthering Waves)", TagCategory.Character),
        "chixia" to TagAlias("chixia_(wuthering_waves)", "Chixia (Wuthering Waves)", TagCategory.Character),
        "baizhi" to TagAlias("baizhi_(wuthering_waves)", "Baizhi (Wuthering Waves)", TagCategory.Character),
        "sanhua" to TagAlias("sanhua_(wuthering_waves)", "Sanhua (Wuthering Waves)", TagCategory.Character),
        "danjin" to TagAlias("danjin_(wuthering_waves)", "Danjin (Wuthering Waves)", TagCategory.Character),
        "calcharo" to TagAlias("calcharo_(wuthering_waves)", "Calcharo (Wuthering Waves)", TagCategory.Character),
        "verina" to TagAlias("verina_(wuthering_waves)", "Verina (Wuthering Waves)", TagCategory.Character),
        "encore" to TagAlias("encore_(wuthering_waves)", "Encore (Wuthering Waves)", TagCategory.Character),
        "jianxin" to TagAlias("jianxin_(wuthering_waves)", "Jianxin (Wuthering Waves)", TagCategory.Character),
        "xiangli yao" to TagAlias("xiangli_yao_(wuthering_waves)", "Xiangli Yao (Wuthering Waves)", TagCategory.Character),

        // Genshin Impact characters
        "raiden" to TagAlias("raiden_shogun", "Raiden Shogun (Genshin Impact)", TagCategory.Character),
        "raiden shogun" to TagAlias("raiden_shogun", "Raiden Shogun (Genshin Impact)", TagCategory.Character),
        "furina" to TagAlias("furina_(genshin_impact)", "Furina (Genshin Impact)", TagCategory.Character),
        "hutao" to TagAlias("hu_tao_(genshin_impact)", "Hu Tao (Genshin Impact)", TagCategory.Character),
        "hu tao" to TagAlias("hu_tao_(genshin_impact)", "Hu Tao (Genshin Impact)", TagCategory.Character),
        "nahida" to TagAlias("nahida_(genshin_impact)", "Nahida (Genshin Impact)", TagCategory.Character),
        "ganyu" to TagAlias("ganyu_(genshin_impact)", "Ganyu (Genshin Impact)", TagCategory.Character),
        "yelan" to TagAlias("yelan_(genshin_impact)", "Yelan (Genshin Impact)", TagCategory.Character),
        "navia" to TagAlias("navia_(genshin_impact)", "Navia (Genshin Impact)", TagCategory.Character),
        "arlecchino" to TagAlias("arlecchino_(genshin_impact)", "Arlecchino (Genshin Impact)", TagCategory.Character),
        "clorinde" to TagAlias("clorinde_(genshin_impact)", "Clorinde (Genshin Impact)", TagCategory.Character),
        "xianyun" to TagAlias("xianyun_(genshin_impact)", "Xianyun (Genshin Impact)", TagCategory.Character),
        "mavuika" to TagAlias("mavuika_(genshin_impact)", "Mavuika (Genshin Impact)", TagCategory.Character),
        "xilonen" to TagAlias("xilonen_(genshin_impact)", "Xilonen (Genshin Impact)", TagCategory.Character),
        "chasca" to TagAlias("chasca_(genshin_impact)", "Chasca (Genshin Impact)", TagCategory.Character),
        "ayaka" to TagAlias("kamisato_ayaka", "Kamisato Ayaka (Genshin Impact)", TagCategory.Character),
        "kamisato ayaka" to TagAlias("kamisato_ayaka", "Kamisato Ayaka (Genshin Impact)", TagCategory.Character),
        "keqing" to TagAlias("keqing_(genshin_impact)", "Keqing (Genshin Impact)", TagCategory.Character),
        "yoimiya" to TagAlias("yoimiya_(genshin_impact)", "Yoimiya (Genshin Impact)", TagCategory.Character),
        "shenhe" to TagAlias("shenhe_(genshin_impact)", "Shenhe (Genshin Impact)", TagCategory.Character),
        "nilou" to TagAlias("nilou_(genshin_impact)", "Nilou (Genshin Impact)", TagCategory.Character),

        // Honkai: Star Rail characters
        "firefly" to TagAlias("firefly_(honkai:_star_rail)", "Firefly (Honkai: Star Rail)", TagCategory.Character),
        "acheron" to TagAlias("acheron_(honkai:_star_rail)", "Acheron (Honkai: Star Rail)", TagCategory.Character),
        "kafka" to TagAlias("kafka_(honkai:_star_rail)", "Kafka (Honkai: Star Rail)", TagCategory.Character),
        "silver wolf" to TagAlias("silver_wolf_(honkai:_star_rail)", "Silver Wolf (Honkai: Star Rail)", TagCategory.Character),
        "silverwolf" to TagAlias("silver_wolf_(honkai:_star_rail)", "Silver Wolf (Honkai: Star Rail)", TagCategory.Character),
        "sparkle" to TagAlias("sparkle_(honkai:_star_rail)", "Sparkle (Honkai: Star Rail)", TagCategory.Character),
        "robin" to TagAlias("robin_(honkai:_star_rail)", "Robin (Honkai: Star Rail)", TagCategory.Character),
        "feixiao" to TagAlias("feixiao_(honkai:_star_rail)", "Feixiao (Honkai: Star Rail)", TagCategory.Character),
        "jingliu" to TagAlias("jingliu_(honkai:_star_rail)", "Jingliu (Honkai: Star Rail)", TagCategory.Character),
        "march 7th" to TagAlias("march_7th_(honkai:_star_rail)", "March 7th (Honkai: Star Rail)", TagCategory.Character),
        "march" to TagAlias("march_7th_(honkai:_star_rail)", "March 7th (Honkai: Star Rail)", TagCategory.Character),
        "ruan mei" to TagAlias("ruan_mei_(honkai:_star_rail)", "Ruan Mei (Honkai: Star Rail)", TagCategory.Character),
        "topaz" to TagAlias("topaz_(honkai:_star_rail)", "Topaz (Honkai: Star Rail)", TagCategory.Character),
        "black swan" to TagAlias("black_swan_(honkai:_star_rail)", "Black Swan (Honkai: Star Rail)", TagCategory.Character),
        "tingyun" to TagAlias("tingyun_(honkai:_star_rail)", "Tingyun (Honkai: Star Rail)", TagCategory.Character),

        // ZZZ characters
        "ellen" to TagAlias("ellen_joe", "Ellen Joe (Zenless Zone Zero)", TagCategory.Character),
        "ellen joe" to TagAlias("ellen_joe", "Ellen Joe (Zenless Zone Zero)", TagCategory.Character),
        "zhuyuan" to TagAlias("zhu_yuan", "Zhu Yuan (Zenless Zone Zero)", TagCategory.Character),
        "zhu yuan" to TagAlias("zhu_yuan", "Zhu Yuan (Zenless Zone Zero)", TagCategory.Character),
        "jane doe" to TagAlias("jane_doe_(zenless_zone_zero)", "Jane Doe (Zenless Zone Zero)", TagCategory.Character),
        "caesar" to TagAlias("caesar_king", "Caesar King (Zenless Zone Zero)", TagCategory.Character),
        "caesar king" to TagAlias("caesar_king", "Caesar King (Zenless Zone Zero)", TagCategory.Character),
        "burnice" to TagAlias("burnice_white", "Burnice White (Zenless Zone Zero)", TagCategory.Character),
        "burnice white" to TagAlias("burnice_white", "Burnice White (Zenless Zone Zero)", TagCategory.Character),
        "miyabi" to TagAlias("hoshimi_miyabi", "Hoshimi Miyabi (Zenless Zone Zero)", TagCategory.Character),
        "hoshimi miyabi" to TagAlias("hoshimi_miyabi", "Hoshimi Miyabi (Zenless Zone Zero)", TagCategory.Character)
    )

    /**
     * Instant local suggestions based on the user's typed token or phrase.
     */
    fun findSuggestions(query: String): List<TagSuggestion> {
        val clean = query.trim().lowercase()
        if (clean.length < 2) return emptyList()

        val results = mutableListOf<TagSuggestion>()
        val seenTags = mutableSetOf<String>()
        val normalizedWithUnderscore = clean.replace(' ', '_')

        // 1. Exact match on alias key
        aliases[clean]?.let { alias ->
            if (seenTags.add(alias.tag)) {
                results.add(TagSuggestion(name = alias.tag, count = 0, category = alias.category, title = alias.title))
            }
        }

        // 2. Prefix match on alias key or clean name
        for ((key, alias) in aliases) {
            if (key.startsWith(clean) || alias.title.lowercase().startsWith(clean) || alias.tag.startsWith(normalizedWithUnderscore)) {
                if (seenTags.add(alias.tag)) {
                    results.add(TagSuggestion(name = alias.tag, count = 0, category = alias.category, title = alias.title))
                }
            }
        }

        // 3. Substring match
        for ((key, alias) in aliases) {
            if (key.contains(clean) || alias.title.lowercase().contains(clean) || alias.tag.contains(normalizedWithUnderscore)) {
                if (seenTags.add(alias.tag)) {
                    results.add(TagSuggestion(name = alias.tag, count = 0, category = alias.category, title = alias.title))
                }
            }
        }

        return results.take(10)
    }

    /**
     * Resolves a freeform search query string (potentially with spaces and commas)
     * into canonical booru tags (e.g. "wuthering wave" -> "wuthering_waves",
     * "azki, solo" -> "azki_(hololive) solo", "wuthering waves changli" -> "wuthering_waves changli_(wuthering_waves)").
     */
    fun resolveSmartQuery(rawQuery: String): String {
        val trimmed = rawQuery.trim()
        if (trimmed.isBlank()) return ""

        // If user used commas, split by comma first
        if (trimmed.contains(',')) {
            return trimmed.split(',')
                .map { it.trim() }
                .filter { it.isNotBlank() }
                .map { resolveSinglePhrase(it) }
                .joinToString(" ")
        }

        // Check if entire query matches an alias
        val fullMatch = aliases[trimmed.lowercase()]
        if (fullMatch != null) {
            return fullMatch.tag
        }

        // Multi-word phrase tokenization with greedy longest-alias matching
        val words = trimmed.split("\\s+".toRegex()).filter { it.isNotBlank() }
        if (words.size <= 1) {
            return resolveSinglePhrase(trimmed)
        }

        val resolvedTokens = mutableListOf<String>()
        var index = 0
        while (index < words.size) {
            // Try matching 3 words, then 2 words, then 1 word
            var matched = false
            for (windowSize in minOf(3, words.size - index) downTo 1) {
                val phrase = words.subList(index, index + windowSize).joinToString(" ").lowercase()
                val alias = aliases[phrase]
                if (alias != null) {
                    resolvedTokens.add(alias.tag)
                    index += windowSize
                    matched = true
                    break
                }
            }
            if (!matched) {
                // Single token fallback
                val single = words[index]
                resolvedTokens.add(resolveSinglePhrase(single))
                index++
            }
        }

        return resolvedTokens.joinToString(" ")
    }

    private fun resolveSinglePhrase(phrase: String): String {
        val clean = phrase.trim()
        val lower = clean.lowercase()
        val alias = aliases[lower]
        if (alias != null) {
            return alias.tag
        }
        // If phrase already has underscore, keep as is
        if (clean.contains('_')) {
            return clean
        }
        // If it's a multi-word phrase without an alias, convert spaces to underscores
        return clean.replace(' ', '_')
    }
}
