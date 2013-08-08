/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-130.js
 * @description Object.defineProperties - 'descObj' is the Math object which implements its own [[Get]] method to get 'value' property (8.10.5 step 5.a)
 */


function testcase() {
        var obj = {};

        try {
            Math.value = "Math";

            Object.defineProperties(obj, {
                property: Math
            });

            return obj.property === "Math";
        } finally {
            delete Math.value;
        }
    }
runTestCase(testcase);
