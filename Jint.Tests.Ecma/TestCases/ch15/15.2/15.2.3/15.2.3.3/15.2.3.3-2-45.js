/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-45.js
 * @description Object.getOwnPropertyDescriptor - argument 'P' is an object which has an own toString and valueOf method
 */


function testcase() {
        var obj = { "bbq": 1, "abc": 2 };
        var valueOfAccessed = false;

        var ownProp = {
            toString: function () {
                return "bbq";
            },
            valueOf: function () {
                valueOfAccessed = true;
                return "abc";
            }
        };

        var desc = Object.getOwnPropertyDescriptor(obj, ownProp);

        return desc.value === 1 && !valueOfAccessed;
    }
runTestCase(testcase);
