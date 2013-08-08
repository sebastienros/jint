/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-45.js
 * @description Object.defineProperty - argument 'P' is an object whose toString method returns an object and whose valueOf method returns a primitive value
 */


function testcase() {
        var obj = {};
        var toStringAccessed = false;
        var valueOfAccessed = false;

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

        Object.defineProperty(obj, ownProp, {});

        return obj.hasOwnProperty("abc") && valueOfAccessed && toStringAccessed;
    }
runTestCase(testcase);
