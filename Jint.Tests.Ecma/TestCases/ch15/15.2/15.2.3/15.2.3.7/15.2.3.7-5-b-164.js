/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-164.js
 * @description Object.defineProperties - value of 'writable' property of 'descObj' is undefined (8.10.5 step 6.b)
 */


function testcase() {
        var obj = {};

        Object.defineProperties(obj, {
            property: {
                writable: undefined
            }
        });

        obj.property = "isWritable";

        return obj.hasOwnProperty("property") && typeof (obj.property) === "undefined";
    }
runTestCase(testcase);
