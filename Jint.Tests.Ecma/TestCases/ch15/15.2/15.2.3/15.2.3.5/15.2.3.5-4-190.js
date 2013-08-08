/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-190.js
 * @description Object.create - 'writable' property of one property in 'Properties' is an inherited accessor property without a get function (8.10.5 step 6.a)
 */


function testcase() {

        var proto = { value: 100 };

        Object.defineProperty(proto, "writable", {
            set: function () { }
        });

        var ConstructFun = function () { };
        ConstructFun.prototype = proto;

        var descObj = new ConstructFun();

        var newObj = Object.create({}, {
            prop: descObj
        });

        var beforeWrite = (newObj.prop === 100);

        newObj.prop = "isWritable";

        var afterWrite = (newObj.prop === 100);

        return beforeWrite === true && afterWrite === true;
    }
runTestCase(testcase);
