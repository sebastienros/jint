/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-7.js
 * @description Object.defineProperties - 'enumerable' property of 'descObj' is not present (8.10.5 step 3)
 */


function testcase() {

        var obj = {};
        var accessed = false;

        Object.defineProperties(obj, {
            prop: {}
        });

        for (var property in obj) {
            if (property === "prop") {
                accessed = true;
            }
        }
        return !accessed;
    }
runTestCase(testcase);
