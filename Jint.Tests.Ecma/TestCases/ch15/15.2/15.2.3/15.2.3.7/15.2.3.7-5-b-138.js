/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-138.js
 * @description Object.defineProperties - 'writable' property of 'descObj' is present (8.10.5 step 6)
 */


function testcase() {
        var obj = {};

        Object.defineProperties(obj, {
            property: {
                writable: false
            }
        });

        obj.property = "isWritable";

        return obj.hasOwnProperty("property") && typeof (obj.property) === "undefined";
    }
runTestCase(testcase);
