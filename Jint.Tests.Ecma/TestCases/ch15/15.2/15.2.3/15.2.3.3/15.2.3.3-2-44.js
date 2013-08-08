/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-2-44.js
 * @description Object.getOwnPropertyDescriptor - argument 'P' is an object that has an own toString method that returns an object and toValue method that returns a primitive value
 */


function testcase() {
        var obj = { "abc": 1 };
        var valueOfAccessed = false;
        var toStringAccessed = false;

        var ownProp = {
            toString: function () {
                toStringAccessed = true;
                return {};
            },
            valueOf: function () {
                valueOfAccessed = true;
                return "abc";
            }
        };

        var desc = Object.getOwnPropertyDescriptor(obj, ownProp);

        return desc.value === 1 && valueOfAccessed && toStringAccessed;
    }
runTestCase(testcase);
