/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-54.js
 * @description Object.defineProperties - value of 'enumerable' property of 'descObj' is the Arguments object (8.10.5 step 3.b)
 */


function testcase() {

        var obj = {};
        var accessed = false;
        var arg; 
        
        (function fun() {
            arg = arguments;
        }(1, 2, 3));

        Object.defineProperties(obj, {
            prop: {
                enumerable: arg
            }
        });
        for (var property in obj) {
            if (property === "prop") {
                accessed = true;
            }
        }
        return accessed;
    }
runTestCase(testcase);
