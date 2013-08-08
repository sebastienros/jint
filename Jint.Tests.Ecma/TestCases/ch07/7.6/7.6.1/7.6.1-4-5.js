/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6.1/7.6.1-4-5.js
 * @description Allow reserved words as property names by set function within an object, verified with hasOwnProperty: finally, return, void
 */


function testcase() {
        var test0 = 0, test1 = 1, test2 = 2;
        var tokenCodes  = {
            set finally(value){
                test0 = value;
            },
            get finally(){
                return test0;
            },
            set return(value){
                test1 = value;
            },
            get return(){
                return test1;
            },
            set void(value){
                test2 = value;
            },
            get void(){
                return test2;
            }
        };      
        var arr = [
            'finally', 
            'return', 
            'void'
            ];
        for(var p in tokenCodes) {       
            for(var p1 in arr) {                
                if(arr[p1] === p) {
                    if(!tokenCodes.hasOwnProperty(arr[p1])) {
                        return false;
                    };
                }
            }
        }
        return true;
    }
runTestCase(testcase);
