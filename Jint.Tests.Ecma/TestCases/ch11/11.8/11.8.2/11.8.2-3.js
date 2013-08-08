/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch11/11.8/11.8.2/11.8.2-3.js
 * @description 11.8.2 Greater-than Operator - Partial left to right order enforced when using Greater-than operator: toString > valueOf
 */


function testcase() {
        var accessed = false;
        var obj1 = {
            toString: function () {
                accessed = true;
                return 3;
            }
        };
        var obj2 = {
            valueOf: function () {
                if (accessed === true) {
                    return 4;
                } else {
                    return 2;
                }
            }
        };
        return !(obj1 > obj2);
    }
runTestCase(testcase);
