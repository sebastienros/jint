/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-34.js
 * @description Object.defineProperties - 'P' doesn't exist in 'O', test [[Set]] of 'P' is set as undefined value if absent in accessor descriptor 'desc' (8.12.9 step 4.b.i)
 */


function testcase() {
        var obj = {};
        var getFunc = function () {
            return 10; 
        };

        Object.defineProperties(obj, {
            prop: {
                get: getFunc,
                enumerable: true,
                configurable: true
            }
        });

        var desc = Object.getOwnPropertyDescriptor(obj, "prop");
        return obj.hasOwnProperty("prop") && typeof (desc.set) === "undefined";

    }
runTestCase(testcase);
