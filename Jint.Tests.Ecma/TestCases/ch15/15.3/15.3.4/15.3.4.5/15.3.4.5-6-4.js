/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-6-4.js
 * @description Function.prototype.bind - F can get own data property that overrides an inherited accessor property
 */


function testcase() {

        var foo = function () { };

        var obj = foo.bind({});
        try {
            Object.defineProperty(Function.prototype, "property", {
                get: function () {
                    return 3;
                },
                configurable: true
            });
           
            Object.defineProperty(obj, "property", {
                value: 12
            });

            return obj.property === 12;
        } finally {
            delete Function.prototype.property;
        }
    }
runTestCase(testcase);
