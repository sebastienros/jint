/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-126.js
 * @description Object.defineProperties - 'descObj' is an Array object which implements its own [[Get]] method to get 'value' property (8.10.5 step 5.a)
 */


function testcase() {
        var obj = {};

        var arr = [1, 2, 3];

        arr.value = "Array";

        Object.defineProperties(obj, {
            property: arr
        });

        return obj.property === "Array";
    }
runTestCase(testcase);
