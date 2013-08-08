/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-217.js
 * @description Object.defineProperties - value of 'get' property of 'descObj' is undefined (8.10.5 step 7.b)
 */


function testcase() {
        var obj = {};

        Object.defineProperties(obj, {
            property: {
                get: undefined
            }
        });

        return obj.hasOwnProperty("property") && typeof obj.property === "undefined";
    }
runTestCase(testcase);
