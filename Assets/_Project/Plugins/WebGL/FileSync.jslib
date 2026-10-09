// Unity WebGL keeps persistentDataPath in an in-memory file system backed by IndexedDB; the backing store is only
// updated when asked. SaveStore calls this after every write so a page refresh or tab close keeps the save.
mergeInto(LibraryManager.library, {
  SyncFiles: function () {
    FS.syncfs(false, function (err) {
      if (err) console.log('[Save] IndexedDB sync failed: ' + err);
    });
  },
});
