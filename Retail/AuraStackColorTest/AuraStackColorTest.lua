local SPELL_ID = 390787
local MAX_TEST_STACKS = 8
local GLYPH = "█"

local function CreateStackFormatter()
    local formatter = C_StringUtil.CreateNumericRuleFormatter()
    local rules = {}

    -- 0/1 层均为白色；从 1 到 8 层线性过渡到黑色。
    for stacks = 0, MAX_TEST_STACKS do
        local visibleStacks = math.max(1, stacks)
        local channel = math.floor(255 * (MAX_TEST_STACKS - visibleStacks)
            / (MAX_TEST_STACKS - 1) + 0.5)
        rules[#rules + 1] = {
            threshold = stacks,
            format = string.format("|cFF%02X%02X%02X%s|r",
                channel, channel, channel, GLYPH),
        }
    end

    formatter:SetBreakpoints(rules)
    return formatter
end

local function ConfigureGlyph(fontString, parent)
    fontString:SetPoint("CENTER", parent, "CENTER", 0, 0)
    fontString:SetJustifyH("CENTER")
    fontString:SetJustifyV("MIDDLE")
    fontString:SetFontHeight(88)
    fontString:SetFixedColor(false)
end

local function Initialize()
    if not C_AddOns.IsAddOnLoaded("Blizzard_AuraContainer") then
        local loaded, reason = C_AddOns.LoadAddOn("Blizzard_AuraContainer")
        if not loaded then
            print("|cFFFF4040[AuraStackColorTest]|r 无法加载 Blizzard_AuraContainer: "
                .. tostring(reason))
            return
        end
    end

    local panel = CreateFrame("Frame", "AuraStackColorTestPanel", UIParent)
    panel:SetSize(132, 132)
    panel:SetPoint("CENTER", UIParent, "CENTER", 0, 0)
    panel:SetFrameStrata("TOOLTIP")

    -- 中灰底板让白色和黑色的完整字符都清晰可见。
    local background = panel:CreateTexture(nil, "BACKGROUND")
    background:SetAllPoints(panel)
    background:SetColorTexture(0.45, 0.45, 0.45, 0.95)

    -- 无光环时保留灰色字符，便于确认插件已经加载。
    local idleGlyph = panel:CreateFontString(nil, "ARTWORK", "GameFontNormal")
    ConfigureGlyph(idleGlyph, panel)
    idleGlyph:SetTextColor(0.7, 0.7, 0.7, 1)
    idleGlyph:SetText(GLYPH)

    local formatter = CreateStackFormatter()
    local container = CreateFrame("AuraContainer", "AuraStackColorTestContainer",
        panel, "CustomAuraContainerTemplate")
    container:SetSize(132, 132)
    container:SetPoint("CENTER", panel, "CENTER", 0, 0)
    container:SetFrameLevel(panel:GetFrameLevel() + 1)
    container:SetUnit("player")

    container:AddAuraSlot("weal_and_woe", "HELPFUL|PLAYER", {
        candidateFilters = {
            includeSpellIDs = { [SPELL_ID] = true },
        },
        initializeFrame = function(button)
            button:SetSize(132, 132)
            button:SetPoint("CENTER", panel, "CENTER", 0, 0)
            button:SetMouseMotionEnabled(false)

            local glyph = button:CreateFontString(nil, "ARTWORK", "GameFontNormal")
            ConfigureGlyph(glyph, button)
            button:SetApplicationCount(glyph, { formatter = formatter })
        end,
    })

    container:SetEnabled(true)
    print("|cFF80D0FF[AuraStackColorTest]|r 正在监测玩家光环 390787；1 层白色，8 层及以上黑色。")
end

local loader = CreateFrame("Frame")
loader:RegisterEvent("PLAYER_LOGIN")
loader:SetScript("OnEvent", function(self)
    self:UnregisterEvent("PLAYER_LOGIN")
    Initialize()
end)
