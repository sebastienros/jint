/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-42.js
 * @description Object.getOwnPropertyDescriptor - argument 'P' is an object which has an own toString method
 */


function testcase() {
        var obj = { "abc": 1 };

        var ownProp = {
            toString: function () {
                return "abc";
            }
        };

        var desc = Object.getOwnPropertyDescriptor(obj, ownProp);

        return desc.value === 1;
    }
runTestCase(testcase);
