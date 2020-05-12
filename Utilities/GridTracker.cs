using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuantumHangar.Utilities
{
    /*
     * This will handle the backup and restore/deletion depending on server save.
     * 
     * 
     * Part A:
     * For checking if a grid got sent to hangar, then server crashed and got rolled back to where grid exists:
     *  We will have a hangar save queue PER server. This will be cleared on server save success.
     *  On success, we will clear the save queue.
     *  
     *  Once server loads after crash, we will read the non-empty save queue and go through and delete those grids from peoples hangars
     *  
     *  If a grid gets loaded after save and before crash, we delete the grid from the delete queue. (grid would no longer be in their hangar)
     *  
     *  
     *  Part B:
     * For checking if a grid got pasted in, then server rolled back, then grid doesnt exist:
     *      -We will have to roll back players hangars. and their grids. 
     *      -Do this by having a hangar delete queue. Everytime a grid gets loaded, we send the gridstamp to player backups in their info file.
     *      -SBC file wont be deleted until server save success
     *      
     *      -Once a grid gets saved, we have to check if that grid is in the hangar delete queue, and remove it
     *
     * 
     * 
     * 
     * 
     * 
     */


    class GridTracker
    {
    }
}
