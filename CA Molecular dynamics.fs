/*{
    "CATEGORIES": [
        "Filter",
        "Generator"
    ],
    "CREDIT": "Mykhailo Moroz <https://www.shadertoy.com/user/michael0884>",
    "DESCRIPTION": "Cellular automaton molecular dynamics, converted from <https://www.shadertoy.com/view/3s3cWr>",
    "INPUTS": [
        {
            "NAME" : "inputImage",
            "TYPE" : "image"
        },
        {
            "NAME": "inputImageAmount",
            "LABEL": "Input image amount",
            "TYPE": "float",
            "DEFAULT": 0,
            "MIN": 0,
            "MAX": 1
        },
        {
            "NAME": "restart",
            "LABEL": "Restart",
            "TYPE": "event"
        },
        {
            "NAME": "dt",
            "LABEL": "Simulation speed",
            "TYPE": "float",
            "DEFAULT": 0.5,
            "MAX": 10,
            "MIN": 0
        },
        {
            "NAME": "gravityDirection",
            "LABEL": "Gravity direction",
            "TYPE": "point2D",
            "DEFAULT": [0, -1],
            "MIN": [-1, -1],
            "MAX": [1, 1]
        },
        {
            "NAME": "gravityScale",
            "LABEL": "Gravity scale",
            "TYPE": "float",
            "DEFAULT": 0.001,
            "MAX": 1,
            "MIN": 0
        },
        {
            "NAME": "cooling",
            "LABEL": "Cooling",
            "TYPE": "float",
            "DEFAULT": 1.5,
            "MAX": 10,
            "MIN": -10
        },
        {
            "NAME": "maxSpeed",
            "LABEL": "Maximum speed",
            "TYPE": "float",
            "DEFAULT": 1,
            "MAX": 10,
            "MIN": 0
        },
        {
            "NAME": "radius",
            "LABEL": "Smoothing radius",
            "TYPE": "float",
            "DEFAULT": 1,
            "MAX": 20,
            "MIN": 0
        },
        {
            "NAME": "velocityContribution",
            "LABEL": "Velocity color contribution",
            "TYPE": "float",
            "DEFAULT": 0,
            "MAX": 10,
            "MIN": 0
        }
    ],
    "ISFVSN": "2",
    "PASSES": [
        {
            "TARGET": "bufferA_positionAndMass",
            "PERSISTENT": true,
            "FLOAT": true
        },
        {
            "TARGET": "bufferA_velocity",
            "PERSISTENT": true,
            "FLOAT": true
        },
        {
            "TARGET": "bufferB_positionAndMass",
            "PERSISTENT": true,
            "FLOAT": true
        },
        {
            "TARGET": "bufferB_velocity",
            "PERSISTENT": true,
            "FLOAT": true
        },
        {

        }
    ]
}*/

#define INV_SQRT_2 0.7071067811865475244008443621048

// Constants and functions from LYGIA <https://github.com/patriciogonzalezvivo/lygia>
#define PI 3.1415926535897932384626433832795
#define TWO_PI 6.2831853071795864769252867665590

float gaussian( vec2 d, float s) { return exp(-( d.x*d.x + d.y*d.y) / (2.0 * s*s)); }

#define saturate(V) clamp(V, 0.0, 1.0)
vec3 hue2rgb(const in float hue) {
    float R = abs(hue * 6.0 - 3.0) - 1.0;
    float G = 2.0 - abs(hue * 6.0 - 2.0);
    float B = 2.0 - abs(hue * 6.0 - 4.0);
    return saturate(vec3(R,G,B));
}
vec3 hsv2rgb(const in vec3 hsv) { return ((hue2rgb(hsv.x) - 1.0) * hsv.y + 1.0) * hsv.z; }
vec4 hsv2rgb(const in vec4 hsv) { return vec4(hsv2rgb(hsv.rgb), hsv.a); }

float luminance(in vec3 linear) { return dot(linear, vec3(0.21250175, 0.71537574, 0.07212251)); }
float luminance(in vec4 linear) { return luminance( linear.rgb ); }

float rectSDF(vec2 p, vec2 b, float r) {
    vec2 d = abs(p - 0.5) * 4.2 - b + vec2(r);
    return min(max(d.x, d.y), 0.0) + length(max(d, 0.0)) - r;
}
float rectSDF(vec2 p, vec2 b) {
    // Why the LYGIA function shifts by 0.5 and scales by 4.2 is a complete mystery.
    return rectSDF((p + 0.5) / 4.2, b, 0.);
}


//
// ShaderToy Common
//

float scalarStep(vec2 x) // Ha in ShaderToy
{
    vec2 r = step(0., x);
    return r.x * r.y;
}

float scalarReflectedStep(vec2 x) // Hb in ShaderToy
{
    vec2 r = vec2(1) - step(x, vec2(0));
    return r.x * r.y;
}

// Particle distribution
vec3 particleDistribution(vec2 x, vec2 pos, vec2 offset)
{
    return vec3(x, 1) * scalarStep(x - (pos - offset)) * scalarReflectedStep((pos + offset) - x);
}

vec3 particleDistribution(vec2 x, vec2 pos)
{
    return particleDistribution(x, pos, vec2(0.5));
}


// The ShaderToy shader uses the functions `floatBitsToUint` and
// `uintBitsToFloat` to pack more than 4 floats (5 in this case) into a
// 4-component pixel. These functions are available in GLSL v3.30 (OpenGL v3.3)
// and later, but some ISF hosts (notably Videosync) use GLSL v1.50
// (OpenGL v3.2). We can work around this by effectively running ShaderToy
// buffers twice, but the packing operations in the ShaderToy shader also
// perform a `clamp` on the packed data. Without the `clamp` calls, this shader
// seems to blow up numerically.
#define POST_UNPACK(X) (clamp(X, 0., 1.) * 2. - 1.)
#define PRE_PACK(X) clamp(0.5 * X + 0.5, 0., 1.)


float border(vec2 p) // In ShaderToy buffer B
{
    float bound = -rectSDF(p - RENDERSIZE * 0.5, RENDERSIZE * vec2(0.49, 0.49));
    // float box = rectSDF((p - RENDERSIZE * vec2(0.5, 0.6)), RENDERSIZE * vec2(0.05, 0.01));
    // float drain = -rectSDF(p - RENDERSIZE * vec2(0.5, 0.7), RENDERSIZE * vec2(0));
    return bound;
}


void main()
{
    vec2 position = gl_FragCoord.xy;

    if (PASSINDEX == 0 || PASSINDEX == 1) // ShaderToy Buffer A
    {
        vec2 X = vec2(0);
        vec2 V = vec2(0);
        float M = 0.;

        // Basically integrate over all updated neighbor distributions that fall
        // inside of this pixel. This makes the tracking conservative.
        for (int i = -2; i <= 2; i++)
        for (int j = -2; j <= 2; j++) {
            vec2 translatedPosition = position + vec2(i, j);
            vec2 wrappedPosition = mod(translatedPosition, RENDERSIZE);
            vec4 data = IMG_PIXEL(bufferB_positionAndMass, wrappedPosition);

            vec2 X0 = POST_UNPACK(data.xy) + translatedPosition;
            vec2 V0 = POST_UNPACK(IMG_PIXEL(bufferB_velocity, wrappedPosition).xy);
           	int M0 = int(data.z);

            X0 += V0 * dt; // Integrate position

            // Deposited mass into this cell
            vec3 m;
            if (M0 >= 2) {
                int halfM0 = M0 / 2;
                m = float(halfM0)      * particleDistribution(X0 + vec2(0.5, 0), position) +
                    float(M0 - halfM0) * particleDistribution(X0 - vec2(0.5, 0), position);
            } else {
                m = float(M0) * particleDistribution(X0, position);
            }

            // Add weighted by mass
            X += m.xy;
            V += V0 * m.z;

            // Add mass
            M += m.z;
        }

        // Normalization
        if (M != 0.) {
            X /= M;
            V /= M;
        }

        // Initial condition
        if (FRAMEINDEX < 1 || restart) {
            X = position;
            V = vec2(0);
            M = mix(
                particleDistribution(position, RENDERSIZE * 0.5, vec2(RENDERSIZE.x * 0.15)).z,
                luminance(IMG_PIXEL(inputImage, position)),
                inputImageAmount
            );
        }

        if (PASSINDEX == 0) {
            X = X - position;
            gl_FragColor = vec4(PRE_PACK(X), M, 1);
        } else {
            gl_FragColor = vec4(PRE_PACK(V), 0, 1);
        }
    }
    else if (PASSINDEX == 2 || PASSINDEX == 3) // ShaderToy Buffer B
    {
        vec2 wrappedPosition = mod(position, RENDERSIZE);
        vec4 data = IMG_PIXEL(bufferA_positionAndMass, wrappedPosition);
        vec2 X = POST_UNPACK(data.xy) + position;
        vec2 V = POST_UNPACK(IMG_PIXEL(bufferA_velocity, wrappedPosition).xy);
        float M = data.z;

        if (M != 0.) { // Not vacuum
            // Compute the force
            vec2 Fa = vec2(0);

            for (int i = -2; i <= 2; i++)
            for (int j = -2; j <= 2; j++) {
                vec2 translatedPosition = position + vec2(i, j);
                vec2 wrappedPosition = mod(translatedPosition, RENDERSIZE);
                vec4 data = IMG_PIXEL(bufferA_positionAndMass, wrappedPosition);

                vec2 X0 = POST_UNPACK(data.xy) + translatedPosition;
                vec2 V0 = POST_UNPACK(IMG_PIXEL(bufferA_velocity, wrappedPosition).xy);
                float M0 = data.z;
                vec2 dx = X0 - X;

                Fa += M0 * (-gaussian(0.75 * dx, INV_SQRT_2) + 0.13 * gaussian(0.4 * dx, INV_SQRT_2)) * dx;
            }

            vec2 F = vec2(0);
            // if (iMouse.z > 0.) {
            //     vec2 dx = pos - iMouse.xy;
            //     F -= 0.003 * dx * gaussian(dx / 30., INV_SQRT_2);
            // }

           	// Gravity
            F += gravityScale * gravityDirection;

            // Integrate velocity
            V += (F + Fa) * dt / M;

            // Wyatt thermostat
            X += cooling * Fa * dt / M;

#define h 1.
            vec3 r = vec3( 1./h,     0, 0.25) * border(X + vec2( h,  0)) +
                     vec3(-1./h,     0, 0.25) * border(X + vec2(-h,  0)) +
                     vec3(    0,  1./h, 0.25) * border(X + vec2( 0,  h)) +
                     vec3(    0, -1./h, 0.25) * border(X + vec2( 0, -h));
            vec3 BORD = vec3(normalize(r.xy), r.z + 1e-4);
            V += 0.5 * smoothstep(0., 5., -BORD.z) * BORD.xy;

            // Velocity limit
            float v = length(V);
            if (v > maxSpeed) {
                V /= v;
            }
        }

        if (PASSINDEX == 2) {
            X = X - position;
            gl_FragColor = vec4(PRE_PACK(X), M, 1);
        } else {
            gl_FragColor = vec4(PRE_PACK(V), 0, 1);
        }
    }
    else // ShaderToy Image
    {
        float rho = 0.001;
        vec2 vel = vec2(0);

        // Compute the smoothed density and velocity
        for (int i = -2; i <= 2; i++)
        for (int j = -2; j <= 2; j++) {
            vec2 translatedPosition = floor(position) + vec2(i, j);
            vec2 wrappedPosition = mod(translatedPosition, RENDERSIZE);
            vec4 data = IMG_PIXEL(bufferA_positionAndMass, wrappedPosition);

            vec2 X0 = POST_UNPACK(data.xy) + translatedPosition;
            vec2 V0 = POST_UNPACK(IMG_PIXEL(bufferB_velocity, wrappedPosition).xy);
            float M0 = data.z;
            vec2 dx = X0 - position;

            float K = gaussian(dx / radius, radius * INV_SQRT_2);
            rho += M0 * K;
            vel += M0 * K * V0;
        }

        vel /= rho;
        vec3 vc = hsv2rgb(vec3(6. * atan(vel.x, vel.y) / TWO_PI, 1, rho * length(vel.xy)));
        gl_FragColor.rgb = cos(0.9 * vec3(3, 2, 1) * rho) + velocityContribution * vc;
    }
}
