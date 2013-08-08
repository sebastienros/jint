/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-185.js
 * @description Object.create - 'writable' property of one property in 'Properties' is an inherited accessor property (8.10.5 step 6.a)
 */


function testcase() {

        var proto = {};

        Object.defineProperty(proto, "writable", {
            get: function () {
                return true;
            }
        });

        var ConstructFun = function () { };
        ConstructFun.prototype = proto;

        var descObj = new ConstructFun();

        var newObj = Object.create({}, {
            prop: descObj
        });

        var beforeWrite = (newObj.hasOwnProperty("prop") && typeof (newObj.prop) === "undefined");

        newObj.prop = "isWritable";

        var afterWrite = (newObj.prop === "isWritable");

        return beforeWrite === true && afterWrite === true;
    }
runTestCase(testcase);
