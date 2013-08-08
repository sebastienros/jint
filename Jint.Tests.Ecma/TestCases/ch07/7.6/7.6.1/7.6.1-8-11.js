/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6.1/7.6.1-8-11.js
 * @description Allow reserved words as property names by set function within an object, accessed via indexing: enum, extends, super
 */


function testcase() {
        var test0 = 0, test1 = 1, test2 = 2;
        var tokenCodes  = {
            set enum(value){
                test0 = value;
            },
            get enum(){
                return test0;
            },
            set extends(value){
                test1 = value;
            },
            get extends(){
                return test1;
            },
            set super(value){
                test2 = value;
            }, 
            get super(){
                return test2;
            }
        }; 
        var arr = [
            'enum',
            'extends',
            'super'
        ];
        for (var i = 0; i < arr.length; i++) {
            if (tokenCodes[arr[i]] !== i) {
                return false;
            };
        }
        return true;
    }
runTestCase(testcase);
