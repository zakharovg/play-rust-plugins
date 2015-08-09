PLUGIN.Name = "Economics VIP"
PLUGIN.Title = "Economics VIP"
PLUGIN.Version = V(0, 1, 0)
PLUGIN.Description = "VIP for Economy System."
PLUGIN.Author = "baton"
PLUGIN.HasConfig = true
PLUGIN.ResourceId = 719

local VipUsers, settings, API = {}, {}, {} 

function PLUGIN:Init() 

	if GetEconomyAPI then
		API = GetEconomyAPI() -- Global Function!
	else
		print("Economics not found!")
	end
	
	self:LoadDefaultConfig()
	VipUsers = datafile.GetDataTable( "Vip" ) or {}
	command.AddChatCommand("vip", self.Plugin, "VipCommandHandler")
	datafile.SaveDataTable( "Vip" )

	timer.Repeat(settings.General.CheckVipTimerInterval, 0, function()
		self:CheckUserList()
	end, self.Plugin)
end

function PLUGIN:LoadDefaultConfig()
    self.Config.Settings = self.Config.Settings or {}
    settings = self.Config.Settings

    settings.Group = settings.Group or {}
    settings.Group.Name = settings.Group.Name or "vip"
    settings.Group.Title = settings.Group.Title or "vip"
    settings.Group.Rank = settings.Group.Rank or 0

    settings.General = settings.General or {}
    settings.General.RequiredBalance = settings.General.RequiredBalance or 350
    settings.General.VipDuration = settings.General.VipDuration or 30 * 24 * 60 * 60
    settings.General.CheckVipTimerInterval = settings.General.CheckVipTimerInterval or 600

    self:SaveConfig()
end

function PLUGIN:CheckUserList()
	local now = time.GetCurrentTime()
	
	for key in pairs(VipUsers) do
		local expirationDateStamp = VipUsers[key]
		local ticks = time.GetCurrentTime().Ticks - expirationDateStamp;
	
		if ticks > 0 then
			permission.RemoveUserGroup(key, settings.Group.Name)
			VipUsers[key] = nil;
		end
	end
	
	datafile.SaveDataTable( "Vip" )
end

function PLUGIN:VipCommandHandler(player, cmd, args) 
	
	datafile.SaveDataTable( "Vip" )
	if player == nil then
		return
	end

	if not permission.GroupExists(settings.Group.Name) then
		permission.CreateGroup(settings.Group.Name, settings.Group.Title, settings.Group.Rank)
	end

	local uid = rust.UserIDFromPlayer(player)
	if VipUsers[uid] ~= nil then
		local expirationDateStamp = VipUsers[uid]
		local ticks = time.GetCurrentTime().Ticks - expirationDateStamp;
		local expirationDate = time.GetCurrentTime().AddTicks(ticks);
		rust.SendChatMessage(player, "Vip", "Your vip status expires at " .. expirationDate:ToString())
		return
	end

	local userdata = API:GetUserData(uid)
	local balance = userdata[1]

	if balance < settings.General.RequiredBalance then
		rust.SendChatMessage(player, "Vip", "You do not have enough money for vip status: " .. settings.General.RequiredBalance)
		return
	end

	if not userdata:Withdraw(settings.General.RequiredBalance) then
		rust.SendChatMessage(player, "Vip", "Error: Cannot withdraw money from your account")
		return
	end

	API.SaveData()

	permission.AddUserGroup(uid, settings.Group.Name)
	VipUsers[uid] = time.GetCurrentTime():AddSeconds(settings.General.VipDuration).Ticks;
	datafile.SaveDataTable( "Vip" )
	rust.SendChatMessage(player, "Vip", "You have become vip!")

end
