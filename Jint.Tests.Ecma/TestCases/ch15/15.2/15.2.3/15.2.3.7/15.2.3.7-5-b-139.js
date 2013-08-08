/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-139.js
 * @description Object.defineProperties - 'writable' property of 'descObj' is not present (8.10.5 step 6)
 */


function testcase() {
        var obj = {};

        Object.defineProperties(obj, {
            property: {
                value: "abc"
            }
        });

        obj.property = "isWritable";

        return obj.property === "abc";
    }
runTestCase(testcase);
