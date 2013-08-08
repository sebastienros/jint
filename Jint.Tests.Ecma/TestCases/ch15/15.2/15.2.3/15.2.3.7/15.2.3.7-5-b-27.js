/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-27.js
 * @description Object.defineProperties - 'descObj' is the JSON object which implements its own [[Get]] method to get 'enumerable' property (8.10.5 step 3.a)
 */


function testcase() {

        var obj = {};
        var accessed = false;

        try {
            JSON.enumerable = true;

            Object.defineProperties(obj, {
                prop: JSON
            });
            for (var property in obj) {
                if (property === "prop") {
                    accessed = true;
                }
            }
            return accessed;
        } finally {
            delete JSON.enumerable;
        }
    }
runTestCase(testcase);
