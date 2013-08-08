/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch11/11.8/11.8.3/11.8.3-5.js
 * @description 11.8.3 Less-than-or-equal Operator - Partial left to right order enforced when using Less-than-or-equal operator: valueOf <= valueOf
 */


function testcase() {
        var accessed = false;
        var obj1 = {
            valueOf: function () {
                accessed = true;
                return 3;
            }
        };
        var obj2 = {
            valueOf: function () {
                if (accessed === true) {
                    return 3;
                } else {
                    return 2;
                }
            }
        };
        return (obj1 <= obj2);
    }
runTestCase(testcase);
