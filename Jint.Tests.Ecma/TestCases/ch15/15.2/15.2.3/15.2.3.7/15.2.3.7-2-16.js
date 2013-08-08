/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-2-16.js
 * @description Object.defineProperties - argument 'Properties' is the Arguments object
 */


function testcase() {

        var obj = {};
        var result = false;

        var Fun = function () {
            return arguments;
        };        
        var props = new Fun();

        Object.defineProperty(props, "prop", {
            get: function () {
                result = ('[object Arguments]' === Object.prototype.toString.call(this));
                return {};
            },
            enumerable: true
        });

        Object.defineProperties(obj, props);
        return result;
    }
runTestCase(testcase);
