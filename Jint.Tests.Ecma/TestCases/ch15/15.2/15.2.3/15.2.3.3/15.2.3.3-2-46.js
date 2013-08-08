/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-46.js
 * @description Object.getOwnPropertyDescriptor - TypeError exception was thrown  when 'P' is an object that both toString and valueOf wouldn't return primitive value
 */


function testcase() {
        var obj = { "1": 1 };
        var toStringAccessed = false;
        var valueOfAccessed = false;

        var ownProp = {
            toString: function () {
                toStringAccessed = true;
                return [1];
            },
            valueOf: function () {
                valueOfAccessed = true;
                return [1];
            }
        };

        try {
            Object.getOwnPropertyDescriptor(obj, ownProp);
            return false;
        } catch (e) {
            return toStringAccessed && valueOfAccessed && e instanceof TypeError;
        }
    }
runTestCase(testcase);
