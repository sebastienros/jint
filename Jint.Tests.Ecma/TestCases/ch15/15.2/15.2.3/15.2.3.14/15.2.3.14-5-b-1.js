/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-5-b-1.js
 * @description Object.keys - Verify that 'index' of returned array is ascend by 1
 */


function testcase() {
        var obj = { prop1: 100, prop2: 200, prop3: 300 };

        var array = Object.keys(obj);

        var idx = 0;
        for (var index in array) {
            if (array.hasOwnProperty(index)) {
                if (index !== idx.toString()) {
                    return false;
                }
                idx++;
            }
        }

        return true;
    }
runTestCase(testcase);
