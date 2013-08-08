/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-28.js
 * @description Object.defineProperties - 'descObj' is an Error object which implements its own [[Get]] method to get 'enumerable' property (8.10.5 step 3.a)
 */


function testcase() {

        var obj = {};
        var descObj = new Error();
        var accessed = false;

        descObj.enumerable = true;

        Object.defineProperties(obj, {
            prop: descObj
        });
        for (var property in obj) {
            if (property === "prop") {
                accessed = true;
            }
        }
        return accessed;
    }
runTestCase(testcase);
