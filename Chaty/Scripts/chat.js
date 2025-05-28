$(function () {
    var chatHub = $.connection.chatHub;
    var currentUser = "";
    var currentChatTarget = null;
    var chatHistory = {}; 
    var unreadMessages = {};
    var allUsers = [];
    var onlineUsers = []; 

    var $userNameTitle = $('#userNameTitle');
    var $userList = $('#userList');
    var $chatHeader = $('#chatHeader');
    var $chatWindow = $('#chatWindow');
    var $messageBox = $('#messageBox');
    var $sendBtn = $('#sendBtn');

    function htmlEncode(value) {
        return $('<div/>').text(value).html();
    }
    function formatTime(dateStr) {
        var date = (dateStr instanceof Date) ? dateStr : new Date(dateStr);
        return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    }

    function updateUserListUI() {
        var $userListContainer = $userList.find('.user-list-container');
        if ($userListContainer.length === 0) {
            $userList.append('<div class="user-list-container"></div>');
            $userListContainer = $userList.find('.user-list-container');
        } else {
            $userListContainer.empty();
        }

        $userNameTitle.text(htmlEncode(currentUser));

        $.each(allUsers, function (i, name) {
            if (name !== currentUser) {
                var unreadCount = unreadMessages[name] || 0;
                var isOnline = onlineUsers.includes(name); 
                var label = htmlEncode(name) + (unreadCount > 0 ? ' <span class="badge">' + unreadCount + '</span>' : '');
                var profileIcon = htmlEncode(name.charAt(0).toUpperCase());

                var userItem = $('<div class="chat-list-item" data-username="' + htmlEncode(name) + '">')
                    .append('<div class="profile-icon ' + (isOnline ? 'online' : 'offline') + '">' + profileIcon + '</div>')
                    .append('<span>' + label + '</span>');

                userItem.click(function () {
                    var targetName = $(this).data('username');
                    currentChatTarget = targetName;
                    $chatHeader.text("Chat with " + htmlEncode(targetName));
                    $chatWindow.empty();

                    console.log("Requesting history for " + targetName);
                    chatHub.server.getChatHistory(targetName);

                    unreadMessages[targetName] = 0;
                    $('.chat-list-item[data-username="' + targetName + '"]').addClass('active');
                    updateUserListUI(); 
                    $messageBox.prop('disabled', false).focus();
                    $sendBtn.prop('disabled', false);
                });

                if (name === currentChatTarget) {
                    userItem.addClass('active');
                }

                $userListContainer.append(userItem);
            }
        });
    }

    function displayMessage(from, message, time) {
        var className = (from === currentUser) ? 'you' : 'other';
        $chatWindow.append(
            '<div class="message ' + className + '">' +
            htmlEncode(message) +
            '<div class="timestamp">' + formatTime(time) + '</div>' +
            '</div>'
        );
        $chatWindow.scrollTop($chatWindow[0].scrollHeight);
    }

    chatHub.client.loadAllUsers = function (users) {
        console.log("Received all users:", users);
        allUsers = users;
        updateUserListUI(); 
    };

    chatHub.client.updateOnlineStatus = function (onlineUserNames) {
        console.log("Received online users:", onlineUserNames);

        onlineUserNames.forEach(function (userName) {
            if (allUsers.indexOf(userName) === -1) {
                allUsers.push(userName);
                console.log("Add new User:", userName);
            }
        });

        onlineUsers = onlineUserNames;
        updateUserListUI(); 
    };

    chatHub.client.updateUserList = function (users) {
        allUsers = users;
        updateUserListUI();
    };

    chatHub.client.newUserAdded = function (userName) {
        console.log("New user added via broadcast:", userName);
        if (userName !== currentUser && !allUsers.includes(userName)) {
            allUsers.push(userName);
           
            updateUserListUI(); 
            console.log("Updated allUsers list:", allUsers);
        }
    };

    chatHub.client.loadChatHistory = function (partnerName, messages) {
        console.log("Received history for " + partnerName, messages);
        $chatWindow.empty();
        chatHistory[partnerName] = messages;

        if (messages) {
            messages.forEach(function (msg) {
                displayMessage(msg.from, msg.message, msg.time);
            });
        }
        $chatWindow.scrollTop($chatWindow[0].scrollHeight);
    };

    chatHub.client.receivePrivateMessage = function (senderUserName, receiverUserName, messageContent, timestamp) {
        console.log("Received PM from " + senderUserName);
        var chatPartner = (senderUserName === currentUser) ? receiverUserName : senderUserName;
        var msgTime = new Date(timestamp);

        if (!chatHistory[chatPartner]) chatHistory[chatPartner] = [];
        chatHistory[chatPartner].push({ from: senderUserName, message: messageContent, time: msgTime });

        if (chatPartner === currentChatTarget) {
            displayMessage(senderUserName, messageContent, msgTime);
        } else {
            unreadMessages[chatPartner] = (unreadMessages[chatPartner] || 0) + 1;
            updateUserListUI();
        }
    };

    chatHub.client.notifyError = function (message) {
        alert("Server Error: " + message);
    };

    function startChat() {
        currentUser = prompt("Enter your name:");
        if (!currentUser || currentUser.trim() === "") {
            alert("You must enter a name to chat.");
            return;
        }

        $.connection.hub.qs = { "username": currentUser };

        $.connection.hub.start().done(function () {
            console.log("Connected as " + currentUser);
            $userNameTitle.text(currentUser);

            chatHub.server.getAllUsers();


            $sendBtn.click(function () {
                var message = $messageBox.val();
                if (message && currentChatTarget) {
                    chatHub.server.sendPrivateMessage(currentChatTarget, message);
                    $messageBox.val('').focus();
                }
            });

            $messageBox.keypress(function (e) {
                if (e.which == 13) {
                    $sendBtn.click();
                    return false;
                }
            });

        }).fail(function (err) {
            alert("Could not connect: " + err);
        });
    }

    startChat();
});