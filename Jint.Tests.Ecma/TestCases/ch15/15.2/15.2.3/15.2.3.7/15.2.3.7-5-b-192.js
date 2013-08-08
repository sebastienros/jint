/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-192.js
 * @description Object.defineProperties - 'get' property of 'descObj' is not present (8.10.5 step 7)
 */


function testcase() {
        var obj = {};

        var setter = function () { };

        Object.defineProperties(obj, {
            property: {
                set: setter
            }
        });

        return obj.hasOwnProperty("property") && typeof (obj.property) === "undefined";
    }
runTestCase(testcase);
