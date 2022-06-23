namespace Oculus.Platform.Samples.SimplePlatformSample
{
	using UnityEngine;
	using UnityEngine.UI;
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using Oculus.Platform;
	using Oculus.Platform.Models;

	public class DataEntry : MonoBehaviour
	{

		public Text dataOutput;

        private void Start()
		{
			Core.Initialize();
			checkEntitlement();
		}

        // Update is called once per frame
        private void Update()
		{
			string currentText = GetComponent<InputField>().text;

			if (Input.GetKey(KeyCode.Return))
			{
				if (currentText != "")
				{
					SubmitCommand(currentText);
				}

				GetComponent<InputField>().text = "";
			}

			// Handle all messages being returned
			Request.RunCallbacks();
		}

		private void SubmitCommand(string command)
		{
			string[] commandParams = command.Split(' ');

			if (commandParams.Length > 0)
			{
				switch (commandParams[0])
				{
					case "p":
						if (commandParams.Length > 2)
						{
							createAndJoinPrivateRoom(commandParams[1], commandParams[2]);
						}
						break;
					case "c":
						getCurrentRoom();
						break;
					case "g":
						if (commandParams.Length > 1)
						{
							getRoom(commandParams[1]);
						}
						break;
					case "j":
						if (commandParams.Length > 1)
						{
							joinRoom(commandParams[1]);
						}
						break;
					case "l":
						if (commandParams.Length > 1)
						{
							leaveRoom(commandParams[1]);
						}
						break;
					case "k":
						if (commandParams.Length > 2)
						{
							kickUser(commandParams[1], commandParams[2]);
						}
						break;
					case "m":
						getLoggedInUser();
						break;
					case "u":
						if (commandParams.Length > 1)
						{
							getUser(commandParams[1]);
						}
						break;
					case "d":
						getLoggedInFriends();
						break;
					case "i":
						getInvitableUsers();
						break;
					case "o":
						if (commandParams.Length > 2)
						{
							inviteUser(commandParams[1], commandParams[2]);
						}
						break;
					case "s":
						if (commandParams.Length > 2)
						{
							setRoomDescription(commandParams[1], commandParams[2]);
						}
						break;
					case "w":
						if (commandParams.Length > 3)
						{
							updateRoomDataStore(commandParams[1], commandParams[2], commandParams[3]);
						}
						break;
					case "n":
						getUserNonce();
						break;
					case "e":
						checkEntitlement();
						break;
					case "a":
						if (commandParams.Length > 1)
						{
							getAchievementDefinition(commandParams[1]);
						}
						break;
					case "b":
						if (commandParams.Length > 1)
						{
							getAchievementProgress(commandParams[1]);
						}
						break;
					case "3":
						if (commandParams.Length > 1)
						{
							unlockAchievement(commandParams[1]);
						}
						break;
					case "4":
						if (commandParams.Length > 2)
						{
							addCountAchievement(commandParams[1], commandParams[2]);
						}
						break;
					case "5":
						if (commandParams.Length > 2)
						{
							addFieldsAchievement(commandParams[1], commandParams[2]);
						}
						break;
					case "1":
						if (commandParams.Length > 2)
						{
							writeLeaderboardEntry(commandParams[1], commandParams[2]);
						}
						break;
					case "2":
						if (commandParams.Length > 1)
						{
							getLeaderboardEntries(commandParams[1]);
						}
						break;
					default:
						printOutputLine("Invalid Command");
						break;
				}
			}
		}

        private void getLeaderboardEntries(string leaderboardName)
		{
			Leaderboards.GetEntries(leaderboardName, 10, LeaderboardFilterType.None, LeaderboardStartAt.Top).OnComplete(leaderboardGetCallback);
		}

        private void writeLeaderboardEntry(string leaderboardName, string value)
		{
			byte[] extraData = new byte[] { 0x54, 0x65, 0x73, 0x74 };

			Leaderboards.WriteEntry(leaderboardName, Convert.ToInt32(value), extraData, false).OnComplete(leaderboardWriteCallback);
		}

        private void addFieldsAchievement(string achievementName, string fields)
		{
			Achievements.AddFields(achievementName, fields).OnComplete(achievementFieldsCallback);
		}

        private void addCountAchievement(string achievementName, string count)
		{
			Achievements.AddCount(achievementName, Convert.ToUInt64(count)).OnComplete(achievementCountCallback);
		}

        private void unlockAchievement(string achievementName)
		{
			Achievements.Unlock(achievementName).OnComplete(achievementUnlockCallback);
		}

        private void getAchievementProgress(string achievementName)
		{
			string[] Names = new string[1];
			Names[0] = achievementName;

			Achievements.GetProgressByName(Names).OnComplete(achievementProgressCallback);
		}

        private void getAchievementDefinition(string achievementName)
		{
			string[] Names = new string[1];
			Names[0] = achievementName;

			Achievements.GetDefinitionsByName(Names).OnComplete(achievementDefinitionCallback);
		}

        private void checkEntitlement()
		{
			Entitlements.IsUserEntitledToApplication().OnComplete(getEntitlementCallback);
		}

        private void getUserNonce()
		{
			printOutputLine("Trying to get user nonce");

			Users.GetUserProof().OnComplete(userProofCallback);
		}

        private void createAndJoinPrivateRoom(string joinPolicy, string maxUsers)
		{
			printOutputLine("Trying to create and join private room");
			Rooms.CreateAndJoinPrivate((RoomJoinPolicy)Convert.ToUInt32(joinPolicy), Convert.ToUInt32(maxUsers)).OnComplete(createAndJoinPrivateRoomCallback);
		}

        private void getCurrentRoom()
		{
			printOutputLine("Trying to get current room");
			Rooms.GetCurrent().OnComplete(getCurrentRoomCallback);
		}

        private void getRoom(string roomID)
		{
			printOutputLine("Trying to get room " + roomID);
			Rooms.Get(Convert.ToUInt64(roomID)).OnComplete(getCurrentRoomCallback);
		}

        private void joinRoom(string roomID)
		{
			printOutputLine("Trying to join room " + roomID);
			Rooms.Join(Convert.ToUInt64(roomID), true).OnComplete(joinRoomCallback);
		}

        private void leaveRoom(string roomID)
		{
			printOutputLine("Trying to leave room " + roomID);
			Rooms.Leave(Convert.ToUInt64(roomID)).OnComplete(leaveRoomCallback);
		}

        private void kickUser(string roomID, string userID)
		{
			printOutputLine("Trying to kick user " + userID + " from room " + roomID);
			Rooms.KickUser(Convert.ToUInt64(roomID), Convert.ToUInt64(userID), 10 /*kick duration */).OnComplete(getCurrentRoomCallback);
		}

        private void getLoggedInUser()
		{
			printOutputLine("Trying to get currently logged in user");
			Users.GetLoggedInUser().OnComplete(getUserCallback);
		}

        private void getUser(string userID)
		{
			printOutputLine("Trying to get user " + userID);
			Users.Get(Convert.ToUInt64(userID)).OnComplete(getUserCallback);
		}

        private void getLoggedInFriends()
		{
			printOutputLine("Trying to get friends of logged in user");
			Users.GetLoggedInUserFriends().OnComplete(getFriendsCallback);
		}

        private void getInvitableUsers()
		{
			printOutputLine("Trying to get invitable users");
			Rooms.GetInvitableUsers().OnComplete(getInvitableUsersCallback);
		}

        private void inviteUser(string roomID, string inviteToken)
		{
			printOutputLine("Trying to invite token " + inviteToken + " to room " + roomID);
			Rooms.InviteUser(Convert.ToUInt64(roomID), inviteToken).OnComplete(inviteUserCallback);
		}

        private void setRoomDescription(string roomID, string description)
		{
			printOutputLine("Trying to set description " + description + " to room " + roomID);
			Rooms.SetDescription(Convert.ToUInt64(roomID), description).OnComplete(getCurrentRoomCallback);
		}

        private void updateRoomDataStore(string roomID, string key, string value)
		{
			Dictionary<string, string> kvPairs = new Dictionary<string, string>();
			kvPairs.Add(key, value);

			printOutputLine("Trying to set k=" + key + " v=" + value + " for room " + roomID);
			Rooms.UpdateDataStore(Convert.ToUInt64(roomID), kvPairs).OnComplete(getCurrentRoomCallback);
		}

        private void printOutputLine(String newLine)
		{
			dataOutput.text = "> " + newLine + System.Environment.NewLine + dataOutput.text;
		}

        private void outputRoomDetails(Room room)
		{
			printOutputLine("Room ID: " + room.ID + ", AppID: " + room.ApplicationID + ", Description: " + room.Description);
			int numUsers = (room.UsersOptional != null) ? room.UsersOptional.Count : 0;
			printOutputLine("MaxUsers: " + room.MaxUsers.ToString() + " Users in room: " + numUsers);
			if (room.OwnerOptional != null)
			{
				printOutputLine("Room owner: " + room.OwnerOptional.ID + " " + room.OwnerOptional.OculusID);
			}
			printOutputLine("Join Policy: " + room.JoinPolicy.ToString());
			printOutputLine("Room Type: " + room.Type.ToString());
			
			Message.MessageType.Matchmaking_Enqueue.GetHashCode();

		}

        private void outputUserArray(UserList users)
		{
			foreach (User user in users)
			{
				printOutputLine("User: " + user.ID + " " + user.OculusID + " " + user.Presence + " " + user.InviteToken);
			}
		}


        // Callbacks
        private void userProofCallback(Message<UserProof> msg)
		{
			if (!msg.IsError)
			{
				printOutputLine("Received user nonce generation success");
				UserProof userNonce = msg.Data;
				printOutputLine("Nonce: " + userNonce.Value);
			}
			else
			{
				printOutputLine("Received user nonce generation error");
				Error error = msg.GetError();
				printOutputLine("Error: " + error.Message);
			}

		}

        private void getEntitlementCallback(Message msg)
		{
			if (!msg.IsError)
			{
				printOutputLine("You are entitled to use this app.");
			}
			else
			{
				printOutputLine("You are NOT entitled to use this app.");
			}
		}

        private void leaderboardGetCallback(Message<LeaderboardEntryList> msg)
		{
			if (!msg.IsError)
			{
				printOutputLine("Leaderboard entry get success.");
				var entries = msg.Data;

				foreach (var entry in entries)
				{
					printOutputLine(entry.Rank + ". " + entry.User.OculusID + " " + entry.Score + " " + entry.Timestamp);
				}
			}
			else
			{
				printOutputLine("Received leaderboard get error");
				Error error = msg.GetError();
				printOutputLine("Error: " + error.Message);
			}
		}

        private void leaderboardWriteCallback(Message msg)
		{
			if (!msg.IsError)
			{
				printOutputLine("Leaderboard entry write success.");
				var didUpdate = (Message<bool>)msg;

				if (didUpdate.Data)
				{
					printOutputLine("Score updated.");
				}
				else
				{
					printOutputLine("Score NOT updated.");
				}
			}
			else
			{
				printOutputLine("Received leaderboard write error");
				Error error = msg.GetError();
				printOutputLine("Error: " + error.Message);
			}
		}

        private void achievementFieldsCallback(Message msg)
	   {
		   if (!msg.IsError)
		   {
			   printOutputLine("Achievement fields added.");
		   }
		   else
		   {
			   printOutputLine("Received achievement fields add error");
			   Error error = msg.GetError();
			   printOutputLine("Error: " + error.Message);
		   }
	   }

        private void achievementCountCallback(Message msg)
	   {
		   if (!msg.IsError)
		   {
			   printOutputLine("Achievement count added.");
		   }
		   else
		   {
			   printOutputLine("Received achievement count add error");
			   Error error = msg.GetError();
			   printOutputLine("Error: " + error.Message);
		   }
	   }

        private void achievementUnlockCallback(Message msg)
	   {
		   if (!msg.IsError)
		   {
			   printOutputLine("Achievement unlocked");
		   }
		   else
		   {
			   printOutputLine("Received achievement unlock error");
			   Error error = msg.GetError();
			   printOutputLine("Error: " + error.Message);
		   }
	   }

        private void achievementProgressCallback(Message<AchievementProgressList> msg)
	   {
		   if (!msg.IsError)
		   {
			   printOutputLine("Received achievement progress success");
			   AchievementProgressList progressList = msg.GetAchievementProgressList();

			   foreach (var progress in progressList)
			   {
				   if (progress.IsUnlocked)
				   {
					   printOutputLine("Achievement Unlocked");
				   }
				   else
				   {
					   printOutputLine("Achievement Locked");
				   }
				   printOutputLine("Current Bitfield: " + progress.Bitfield.ToString());
				   printOutputLine("Current Count: " + progress.Count.ToString());
			   }
		   }
		   else
		   {
			   printOutputLine("Received achievement progress error");
			   Error error = msg.GetError();
			   printOutputLine("Error: " + error.Message);
		   }
	   }

        private void achievementDefinitionCallback(Message<AchievementDefinitionList> msg)
	   {
		   if (!msg.IsError)
		   {
			   printOutputLine("Received achievement definitions success");
			   AchievementDefinitionList definitionList = msg.GetAchievementDefinitions();

			   foreach (var definition in definitionList)
			   {
				   switch (definition.Type)
				   {
					   case AchievementType.Simple:
						   printOutputLine("Achievement Type: Simple");
						   break;
					   case AchievementType.Bitfield:
						   printOutputLine("Achievement Type: Bitfield");
						   printOutputLine("Bitfield Length: " + definition.BitfieldLength.ToString());
						   printOutputLine("Target: " + definition.Target.ToString());
						   break;
					   case AchievementType.Count:
						   printOutputLine("Achievement Type: Count");
						   printOutputLine("Target: " + definition.Target.ToString());
						   break;
					   case AchievementType.Unknown:
					   default:
						   printOutputLine("Achievement Type: Unknown");
						   break;
				   }
			   }
		   }
		   else
		   {
			   printOutputLine("Received achievement definitions error");
			   Error error = msg.GetError();
			   printOutputLine("Error: " + error.Message);
		   }
	   }

        private void createAndJoinPrivateRoomCallback(Message<Room> msg)
	   {
		   if (!msg.IsError)
		   {
			   printOutputLine("Received create and join room success");
			   outputRoomDetails(msg.Data);
		   }
		   else
		   {
			   printOutputLine("Received create and join room error");
			   Error error = msg.GetError();
			   printOutputLine("Error: " + error.Message);
		   }
	   }

        private void getCurrentRoomCallback(Message<Room> msg)
	   {
		   if (!msg.IsError)
		   {
			   printOutputLine("Received get room success");
			   outputRoomDetails(msg.Data);
		   }
		   else
		   {
			   printOutputLine("Received get room error");
			   Error error = msg.GetError();
			   printOutputLine("Error: " + error.Message);
		   }
	   }

        private void joinRoomCallback(Message<Room> msg)
	   {
		   if (!msg.IsError)
		   {
			   printOutputLine("Received join room success");
			   outputRoomDetails(msg.Data);
		   }
		   else
		   {
			   printOutputLine("Received join room error");
			   Error error = msg.GetError();
			   printOutputLine("Error: " + error.Message);
		   }
	   }

        private void leaveRoomCallback(Message<Room> msg)
	   {
		   if (!msg.IsError)
		   {
			   printOutputLine("Received leave room success");
			   outputRoomDetails(msg.Data);
		   }
		   else
		   {
			   printOutputLine("Received leave room error");
			   Error error = msg.GetError();
			   printOutputLine("Error: " + error.Message);
		   }
	   }

        private void getUserCallback(Message<User> msg)
	   {
		   if (!msg.IsError)
		   {
			   printOutputLine("Received get user success");
			   User user = msg.Data;
			   printOutputLine("User: " + user.ID + " " + user.OculusID + " " + user.Presence + " " + user.InviteToken);
		   }
		   else
		   {
			   printOutputLine("Received get user error");
			   Error error = msg.GetError();
			   printOutputLine("Error: " + error.Message);
		   }
	   }

        private void getFriendsCallback(Message<UserList> msg)
	   {
		   if (!msg.IsError)
		   {
			   printOutputLine("Received get friends success");
			   UserList users = msg.Data;
			   outputUserArray(users);
		   }
		   else
		   {
			   printOutputLine("Received get friends error");
			   Error error = msg.GetError();
			   printOutputLine("Error: " + error.Message);
		   }
	   }

        private void getInvitableUsersCallback(Message<UserList> msg)
	   {
		   if (!msg.IsError)
		   {
			   printOutputLine("Received get invitable users success");
			   UserList users = msg.Data;
			   outputUserArray(users);
		   }
		   else
		   {
			   printOutputLine("Received get invitable users error");
			   Error error = msg.GetError();
			   printOutputLine("Error: " + error.Message);
		   }
	   }

        private void inviteUserCallback(Message msg)
	   {
		   if (!msg.IsError)
		   {
			   printOutputLine("Received invite user success");
		   }
		   else
		   {
			   printOutputLine("Received invite user error");
			   Error error = msg.GetError();
			   printOutputLine("Error: " + error.Message);
		   }
	   }
	}
}
