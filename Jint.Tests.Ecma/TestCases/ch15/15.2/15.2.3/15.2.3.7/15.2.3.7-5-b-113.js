/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-113.js
 * @description Object.defineProperties - 'value' property of 'descObj' is not present (8.10.5 step 5)
 */


function testcase() {
        var obj = {};

        Object.defineProperties(obj, {
            property: {
                writable: true
            }
        });

        return obj.hasOwnProperty("property") && typeof (obj.property) === "undefined";
    }
runTestCase(testcase);
